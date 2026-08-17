; Common Inno Setup configuration shared between installers
; This file is included by both ClipMate.iss and ClipMate-Portable.iss

[Files]
; Main application files
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Default templates (only install if they don't already exist)
Source: "assets\Templates\*.txt"; DestDir: "{userappdata}\ClipMate\Templates"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
; Start menu shortcut
; {userprograms}, not {autoprograms} -- see the scope note in ClipMate.iss
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\ClipMate.App.exe"; Comment: "Launch {#MyAppName}"

[Registry]
; Optional startup entry (controlled by task)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\ClipMate.App.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Tasks]
; User-selectable options
Name: "startup"; Description: "Start {#MyAppName} when Windows starts"; GroupDescription: "Startup options:"

[Run]
; Launch application after installation
Filename: "{app}\ClipMate.App.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// {localappdata}\ClipMate holds the clip database and settings, NOT installed files.
// It must never be removed unconditionally -- doing so destroys the user's entire clip
// history on an ordinary uninstall, including the uninstall/reinstall cycle people use
// to migrate between install scopes. Ask instead, defaulting to keeping the data.
// MB_DEFBUTTON2 also means a silent uninstall (e.g. `winget uninstall`) suppresses the
// prompt and takes the default -- No -- so automated uninstalls preserve data too.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  DataDir := ExpandConstant('{localappdata}\{#MyAppName}');
  if not DirExists(DataDir) then
    Exit;

  if MsgBox('Also delete your ClipMate clip database and settings?'
            + #13#10#13#10 + DataDir
            + #13#10#13#10 + 'Choose No to keep your clips for a future reinstall.',
            mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    DelTree(DataDir, True, True, True);
end;
