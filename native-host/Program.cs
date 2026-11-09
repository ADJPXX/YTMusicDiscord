using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

namespace NativeHost;

internal static class Program
{
    private const int MaxMessageBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static async Task<int> Main()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (!File.Exists(settingsPath))
        {
            await Console.Error.WriteLineAsync($"Configuração não encontrada: {settingsPath}");
            return 1;
        }

        var settings = JsonSerializer.Deserialize<Settings>(await File.ReadAllTextAsync(settingsPath), JsonOptions);
        if (string.IsNullOrWhiteSpace(settings?.DiscordApplicationId))
        {
            await Console.Error.WriteLineAsync("DiscordApplicationId está ausente no settings.json.");
            return 1;
        }

        await using var rpc = new DiscordRpcClient(settings.DiscordApplicationId);
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();

        try
        {
            while (true)
            {
                var messageBytes = await ReadNativeMessageAsync(input);
                if (messageBytes is null) break;

                NativeResponse response;
                try
                {
                    var message = JsonSerializer.Deserialize<PlaybackMessage>(messageBytes, JsonOptions);
                    if (message is null)
                        throw new InvalidDataException("Mensagem vazia.");

                    if (string.Equals(message.Kind, "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        await rpc.ClearActivityAsync();
                        response = new NativeResponse(true, null);
                    }
                    else if (string.Equals(message.Kind, "track", StringComparison.OrdinalIgnoreCase))
                    {
                        ValidateTrack(message);
                        var delivered = await rpc.SetTrackAsync(message);
                        response = new NativeResponse(delivered, delivered ? null : "Abra o aplicativo Discord para publicar a presença.");
                    }
                    else
                    {
                        throw new InvalidDataException("Tipo de mensagem desconhecido.");
                    }
                }
                catch (Exception exception)
                {
                    await Console.Error.WriteLineAsync(exception.Message);
                    response = new NativeResponse(false, "Não foi possível atualizar a presença.");
                }

                await WriteNativeMessageAsync(output, response);
            }
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"Falha no host de mensagens: {exception}");
            return 1;
        }
        finally
        {
            await rpc.ClearActivityAsync();
        }

        return 0;
    }

    private static void ValidateTrack(PlaybackMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Title))
            throw new InvalidDataException("A faixa não tem título.");

        if (!Uri.TryCreate(message.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Host != "music.youtube.com")
            throw new InvalidDataException("A origem da faixa não é YouTube Music.");
    }

    private static async Task<byte[]?> ReadNativeMessageAsync(Stream input)
    {
        var header = new byte[4];
        var firstByte = await input.ReadAsync(header.AsMemory(0, 1));
        if (firstByte == 0) return null;
        await ReadExactlyAsync(input, header.AsMemory(1));

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > MaxMessageBytes)
            throw new InvalidDataException($"Tamanho de mensagem inválido: {length}.");

        var payload = new byte[length];
        await ReadExactlyAsync(input, payload);
        return payload;
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer)
    {
        while (!buffer.IsEmpty)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0) throw new EndOfStreamException("A mensagem foi interrompida.");
            buffer = buffer[read..];
        }
    }

    private static async Task WriteNativeMessageAsync(Stream output, NativeResponse response)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await output.WriteAsync(header);
        await output.WriteAsync(payload);
        await output.FlushAsync();
    }

    private sealed class Settings
    {
        public string? DiscordApplicationId { get; set; }
    }

    private sealed record NativeResponse(bool Ok, string? Error);
}

internal sealed class PlaybackMessage
{
    public string? Kind { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Url { get; set; }
    public string? ArtworkUrl { get; set; }
    public bool Playing { get; set; }
    public double CurrentTime { get; set; }
    public double Duration { get; set; }
}

internal sealed class DiscordRpcClient : IAsyncDisposable
{
    private const int MaxRpcFrameBytes = 1024 * 1024;
    private readonly string _applicationId;
    private NamedPipeClientStream? _pipe;
    private DateTimeOffset _retryAfter = DateTimeOffset.MinValue;
    private string? _lastSignature;

    public DiscordRpcClient(string applicationId) => _applicationId = applicationId;

    public async Task<bool> SetTrackAsync(PlaybackMessage message)
    {
        if (!await EnsureConnectedAsync()) return false;

        var title = Limit(message.Title, 128);
        var artist = Limit(message.Artist, 128);
        var isPlaying = message.Playing;
        var currentTime = double.IsFinite(message.CurrentTime) ? Math.Max(0, message.CurrentTime) : 0;
        var duration = double.IsFinite(message.Duration) ? Math.Max(0, message.Duration) : 0;
        var playbackBucket = isPlaying ? (long)(currentTime / 20) : 0;
        var signature = $"{title}\u001f{artist}\u001f{isPlaying}\u001f{playbackBucket}";
        if (signature == _lastSignature) return true;

        var state = isPlaying ? artist : $"{artist} · pausada";
        var activity = new Dictionary<string, object?>
        {
            ["name"] = title,
            ["type"] = 2,
            ["status_display_type"] = 2,
            ["details"] = title,
            ["state"] = Limit(state, 128)
        };

        var artworkUrl = GetAllowedArtworkUrl(message.ArtworkUrl);
        if (artworkUrl is not null)
        {
            activity["assets"] = new
            {
                large_image = artworkUrl,
                large_text = Limit($"{title} — {artist}", 128)
            };
        }

        if (isPlaying && duration > currentTime + 1)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            activity["timestamps"] = new
            {
                start = now - (long)currentTime,
                end = now + (long)(duration - currentTime)
            };
        }

        try
        {
            await SendCommandAsync("SET_ACTIVITY", new { pid = Environment.ProcessId, activity });
            _lastSignature = signature;
            return true;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"Falha ao publicar a atividade no Discord: {exception.Message}");
            Disconnect();
            return false;
        }
    }

    public async Task ClearActivityAsync()
    {
        _lastSignature = null;
        if (_pipe is null || !_pipe.IsConnected) return;
        try
        {
            await SendCommandAsync("SET_ACTIVITY", new { pid = Environment.ProcessId, activity = (object?)null });
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"Não foi possível limpar a atividade: {exception.Message}");
            Disconnect();
        }
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        return ValueTask.CompletedTask;
    }

    private async Task<bool> EnsureConnectedAsync()
    {
        if (_pipe?.IsConnected == true) return true;
        if (DateTimeOffset.UtcNow < _retryAfter) return false;
        _retryAfter = DateTimeOffset.UtcNow.AddSeconds(5);

        for (var index = 0; index < 10; index++)
        {
            var candidate = new NamedPipeClientStream(".", $"discord-ipc-{index}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await candidate.ConnectAsync(250);
                var handshake = JsonSerializer.SerializeToUtf8Bytes(new { v = 1, client_id = _applicationId });
                await WriteRpcFrameAsync(candidate, 0, handshake);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                EnsureRpcSuccess(await ReadRpcFrameAsync(candidate, timeout.Token));
                _pipe = candidate;
                _retryAfter = DateTimeOffset.MinValue;
                return true;
            }
            catch
            {
                await candidate.DisposeAsync();
            }
        }

        return false;
    }

    private async Task SendCommandAsync(string command, object args)
    {
        var pipe = _pipe ?? throw new IOException("Discord IPC não está conectado.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            cmd = command,
            args,
            nonce = Guid.NewGuid().ToString("N")
        });
        await WriteRpcFrameAsync(pipe, 1, payload);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        EnsureRpcSuccess(await ReadRpcFrameAsync(pipe, timeout.Token));
    }

    private static void EnsureRpcSuccess(byte[] response)
    {
        using var document = JsonDocument.Parse(response);
        var root = document.RootElement;
        if (!root.TryGetProperty("evt", out var eventName) || eventName.ValueKind != JsonValueKind.String ||
            !string.Equals(eventName.GetString(), "ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var message = root.TryGetProperty("data", out var data) && data.TryGetProperty("message", out var errorMessage)
            ? errorMessage.GetString()
            : null;
        throw new IOException(string.IsNullOrWhiteSpace(message) ? "Discord recusou a requisição RPC." : message);
    }

    private static async Task WriteRpcFrameAsync(Stream stream, int opcode, byte[] payload)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), opcode);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    private static async Task<byte[]> ReadRpcFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        if (length is < 0 or > MaxRpcFrameBytes)
            throw new InvalidDataException($"Tamanho de resposta RPC inválido: {length}.");

        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return payload;
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (!buffer.IsEmpty)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) throw new EndOfStreamException("Discord fechou o canal IPC.");
            buffer = buffer[read..];
        }
    }

    private static string Limit(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value) ? "YouTube Music" : value.Trim()[..Math.Min(value.Trim().Length, maximum)];

    private static string? GetAllowedArtworkUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return null;

        var host = uri.Host;
        var allowed = new[] { "googleusercontent.com", "ytimg.com", "ggpht.com" };
        return allowed.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                                     host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase))
            ? uri.ToString()
            : null;
    }

    private void Disconnect()
    {
        _pipe?.Dispose();
        _pipe = null;
    }
}
