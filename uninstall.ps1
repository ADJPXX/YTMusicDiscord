$ErrorActionPreference = "Stop"
$registryPath = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\org.codex.ytmusic_discord"

if (Test-Path -LiteralPath $registryPath) {
    Remove-Item -LiteralPath $registryPath -Recurse -Force
    Write-Host "Registro do host removido. Você também pode remover a extensão em edge://extensions."
} else {
    Write-Host "O host já estava removido do registro."
}
