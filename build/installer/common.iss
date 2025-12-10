; Common Inno Setup configuration shared between installers
; This file is included by both ClipMate.iss and ClipMate-Portable.iss

[Files]
; Main application files
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start menu shortcut
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\ClipMate.App.exe"; Comment: "Launch {#MyAppName}"

[Registry]
; Optional startup entry (controlled by task)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\ClipMate.App.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Tasks]
; User-selectable options
Name: "startup"; Description: "Start {#MyAppName} when Windows starts"; GroupDescription: "Startup options:"

[Run]
; Launch application after installation
Filename: "{app}\ClipMate.App.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up any user data that wasn't part of the installation
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"
