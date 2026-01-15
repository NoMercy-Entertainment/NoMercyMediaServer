[Setup]
AppName=NoMercy MediaServer
AppVersion=0.5.0
DefaultDirName={pf}\NoMercy MediaServer
DefaultGroupName=NoMercy MediaServer
OutputBaseFilename=NoMercyMediaServerSetup
Compression=lzma
SolidCompression=yes

[Files]
Source: "..\..\\Release\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
Filename: "{app}\NoMercy MediaServer.exe"; Description: "{cm:LaunchProgram,NoMercy MediaServer}"; Flags: nowait postinstall skipifsilent

[Icons]
Name: "{group}\NoMercy MediaServer"; Filename: "{app}\NoMercy MediaServer.exe"
Name: "{group}\{cm:UninstallProgram,NoMercy MediaServer}"; Filename: "{uninstallexe}"

[Run]
Filename: "sc.exe"; Parameters: "create NoMercyMediaServer binPath= ""{app}\NoMercy MediaServer.exe"" start= auto"; Flags: runhidden
Filename: "sc.exe"; Parameters: "start NoMercyMediaServer"; Flags: runhidden