param(
    [string]$ExtensionId,
    [string]$DiscordApplicationId
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ExtensionId)) {
    $ExtensionId = Read-Host "ID da extensão exibido em edge://extensions"
}
if ([string]::IsNullOrWhiteSpace($DiscordApplicationId)) {
    $DiscordApplicationId = Read-Host "Application ID copiado do Discord Developer Portal"
}

if ($ExtensionId -notmatch '^[a-p]{32}$') {
    throw "ID da extensão inválido. Copie os 32 caracteres mostrados em edge://extensions."
}
if ($DiscordApplicationId -notmatch '^\d{17,20}$') {
    throw "Application ID inválido. Ele deve conter apenas os dígitos exibidos no Developer Portal."
}

$projectPath = Join-Path $PSScriptRoot "native-host\NativeHost.csproj"
$publishPath = Join-Path $PSScriptRoot "native-host\publish"
$hostExe = Join-Path $publishPath "YTMusicDiscordHostV3.exe"
$settingsPath = Join-Path $publishPath "settings.json"
$hostManifestPath = Join-Path $publishPath "org.codex.ytmusic_discord.json"
$registryPath = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\org.codex.ytmusic_discord"

if (-not (Test-Path -LiteralPath $hostExe)) {
    Write-Host "Compilando o host C# para Windows x64..."
    & dotnet publish $projectPath --configuration Release --runtime win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -p:DebugSymbols=false -o $publishPath
    if ($LASTEXITCODE -ne 0) {
        throw "A compilação falhou. Instale o .NET 10 SDK e execute o instalador novamente."
    }
}

$settings = @{ discordApplicationId = $DiscordApplicationId } | ConvertTo-Json -Compress
[System.IO.File]::WriteAllText($settingsPath, $settings, [System.Text.UTF8Encoding]::new($false))

$nativeManifest = @{
    name = "org.codex.ytmusic_discord"
    description = "Ponte C# entre YouTube Music no Edge e Discord Rich Presence"
    path = $hostExe
    type = "stdio"
    allowed_origins = @("chrome-extension://$ExtensionId/")
} | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($hostManifestPath, $nativeManifest, [System.Text.UTF8Encoding]::new($false))

New-Item -Path $registryPath -Force | Out-Null
Set-Item -Path $registryPath -Value $hostManifestPath

Write-Host "Instalação concluída."
Write-Host "Host registrado para o Edge. Recarregue a extensão em edge://extensions e atualize a aba do YouTube Music."
