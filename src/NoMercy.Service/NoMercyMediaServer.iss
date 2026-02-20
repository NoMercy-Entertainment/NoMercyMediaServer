#ifndef Version
  #define Version "0.1.0"
#endif

[Setup]
AppName=NoMercy MediaServer
AppVersion={#Version}
AppPublisher=NoMercy Entertainment
AppPublisherURL=https://nomercy.tv
AppSupportURL=https://github.com/NoMercy-Entertainment/NoMercyMediaServer
DefaultDirName={autopf}\NoMercy MediaServer
DefaultGroupName=NoMercy MediaServer
OutputBaseFilename=NoMercyMediaServer-{#Version}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\..\assets\icons\icon.ico
UninstallDisplayIcon={app}\NoMercyMediaServer.exe
PrivilegesRequired=admin
WizardStyle=modern
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation (Server + App + Service + CLI)"
Name: "server"; Description: "Server only"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "server"; Description: "NoMercy MediaServer"; Types: full server custom; Flags: fixed
Name: "app"; Description: "NoMercy App (desktop media interface)"; Types: full
Name: "service"; Description: "NoMercy Launcher (system tray control)"; Types: full
Name: "cli"; Description: "NoMercy CLI (command-line tool)"; Types: full

[Files]
; Shared runtime and dependencies (always installed with server)
Source: "..\..\installer-payload\*.dll"; DestDir: "{app}"; Components: server; Flags: ignoreversion
Source: "..\..\installer-payload\*.json"; DestDir: "{app}"; Components: server; Flags: ignoreversion

; Main executables per component
Source: "..\..\installer-payload\NoMercyMediaServer.exe"; DestDir: "{app}"; Components: server; Flags: ignoreversion
Source: "..\..\installer-payload\NoMercyApp.exe"; DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "..\..\installer-payload\NoMercyLauncher.exe"; DestDir: "{app}"; Components: service; Flags: ignoreversion
Source: "..\..\installer-payload\nomercy.exe"; DestDir: "{app}"; Components: cli; Flags: ignoreversion

; Icon
Source: "..\..\assets\icons\icon.ico"; DestDir: "{app}"; DestName: "icon.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\NoMercy MediaServer"; Filename: "{app}\NoMercyMediaServer.exe"; IconFilename: "{app}\icon.ico"
Name: "{group}\NoMercy MediaServer"; Filename: "{app}\NoMercyLauncher.exe"; IconFilename: "{app}\icon.ico"; Components: service
Name: "{group}\{cm:UninstallProgram,NoMercy MediaServer}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NoMercy MediaServer"; Filename: "{app}\NoMercyLauncher.exe"; IconFilename: "{app}\icon.ico"; Components: service; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Components: service
Name: "installservice"; Description: "Install as Windows Service (auto-start on boot)"; GroupDescription: "Service:"; Components: server
Name: "addtopath"; Description: "Add CLI to system PATH"; GroupDescription: "CLI:"; Components: cli

[Run]
Filename: "sc.exe"; Parameters: "create NoMercyMediaServer binPath= ""{app}\NoMercyMediaServer.exe"" start= auto DisplayName= ""NoMercy MediaServer"""; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "start NoMercyMediaServer"; Flags: runhidden; Tasks: installservice
Filename: "{app}\NoMercyLauncher.exe"; Description: "Launch NoMercy MediaServer"; Flags: nowait postinstall skipifsilent; Components: service

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop NoMercyMediaServer"; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "delete NoMercyMediaServer"; Flags: runhidden; Tasks: installservice

[Registry]
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Tasks: addtopath; Check: NeedsAddPath(ExpandConstant('{app}'))

[Code]
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    'Path', OrigPath)
  then begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;
