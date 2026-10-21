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
const
  FirewallRuleName = 'ClipMate Peer Networking';

// Whether ClipMate's inbound firewall rule exists for ExePath. Reading the rule needs no
// elevation; netsh's output is captured through a temp file because Exec cannot read it.
function FirewallRuleExistsFor(const ExePath: String): Boolean;
var
  OutputFile: String;
  Output: AnsiString;
  ResultCode: Integer;
begin
  Result := False;
  OutputFile := ExpandConstant('{tmp}\clipmate-firewall-rule.txt');
  if not Exec(ExpandConstant('{cmd}'),
              '/C netsh advfirewall firewall show rule name="' + FirewallRuleName + '" verbose > "' + OutputFile + '"',
              '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Exit;

  if (ResultCode = 0) and LoadStringFromFile(OutputFile, Output) then
    Result := Pos(Lowercase(ExePath), Lowercase(String(Output))) > 0;

  DeleteFile(OutputFile);
end;

// ClipMate adds an inbound firewall rule for its own executable the first time Peer
// networking is turned on (through an elevated netsh, since this install is per-user).
// Removing it also needs elevation, so only ask when the rule exists. A silent uninstall
// (e.g. `winget uninstall`) never raises an unexpected permission prompt; the leftover
// rule names a program that no longer exists, so it lets nothing in.
procedure RemoveFirewallRule;
var
  ExePath: String;
  ResultCode: Integer;
begin
  ExePath := ExpandConstant('{app}\ClipMate.App.exe');
  if UninstallSilent or not FirewallRuleExistsFor(ExePath) then
    Exit;

  if MsgBox('ClipMate added a Windows Firewall rule so your paired Devices could connect to it.'
            + #13#10#13#10 + 'Remove the rule now? Windows will ask for permission.',
            mbConfirmation, MB_YESNO) <> IDYES then
    Exit;

  if not ShellExec('runas', 'netsh.exe',
                   'advfirewall firewall delete rule name="' + FirewallRuleName + '" program="' + ExePath + '"',
                   '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('Firewall rule not removed: ' + SysErrorMessage(ResultCode));
end;

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
  // Before the files go, while {app} still names the executable the rule was added for
  if CurUninstallStep = usUninstall then
    RemoveFirewallRule;

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
