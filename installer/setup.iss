#define AppVersion "1.0.2"

[Setup]
AppId=YTMusicDiscordHost
AppName=YouTube Music para Discord - Host
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\YTMusicDiscord
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\release
OutputBaseFilename=YTMusicDiscord-Host-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "..\native-host\publish\YTMusicDiscordHostV3.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "settings.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "org.codex.ytmusic_discord.json"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Edge\NativeMessagingHosts\org.codex.ytmusic_discord"; ValueType: string; ValueName: ""; ValueData: "{app}\org.codex.ytmusic_discord.json"; Flags: uninsdeletekey