; ClipMate Framework-Dependent Installer Script
; Requires .NET 9 Desktop Runtime and WebView2 (downloaded automatically)

#define MyAppName "ClipMate"
#define MyAppPublisher "ClipMate Contributors"
#define MyAppURL "https://github.com/Tsabo/ClipMate"
#define MyAppExeName "ClipMate.App.exe"

; These defines are passed from the build script
; #define MyAppVersion "1.0.0"
; #define SourcePath "path\to\publish"
; #define OutputFilename "ClipMate-Setup-1.0.0"

[Setup]
; Application info
AppId={{8B5C9E7A-1F2D-4A3E-9C8B-7D6F5E4A3B2C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=Copyright © 2025 {#MyAppPublisher}

; Installation paths
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output configuration
OutputDir=.
OutputBaseFilename={#OutputFilename}
SetupIconFile=..\..\Source\src\ClipMate.App\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compression
Compression=lzma2/max
SolidCompression=yes

; UI
WizardStyle=modern

; Requirements
MinVersion=10.0.17763
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest

; License
LicenseFile=..\..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nInstaller size: ~50MB%n%nThis installer requires an internet connection to download:%n• .NET 9 Desktop Runtime%n• Microsoft Edge WebView2 Runtime%n%nIt is recommended that you close all other applications before continuing.

[Code]
function IsDotNet9Installed: Boolean;
var
  KeyExists: Boolean;
begin
  // Check for .NET 9 Desktop Runtime
  KeyExists := RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost') or
               RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedhost');
  
  Result := KeyExists;
  
  if Result then
    Log('.NET 9 Desktop Runtime detected')
  else
    Log('.NET 9 Desktop Runtime NOT detected');
end;

function IsWebView2Installed: Boolean;
var
  Version: String;
begin
  // Check for WebView2 Runtime
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
            RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
  
  if Result then
    Log('WebView2 Runtime detected: ' + Version)
  else
    Log('WebView2 Runtime NOT detected');
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  
  // Check for .NET 9
  if not IsDotNet9Installed then
  begin
    MsgBox('.NET 9 Desktop Runtime is required but not installed.' + #13#10 + #13#10 +
           'The installer will download and install it automatically.' + #13#10 +
           'This requires an active internet connection.' + #13#10 + #13#10 +
           'Download size: ~60-80 MB', 
           mbInformation, MB_OK);
  end;
  
  // Check for WebView2
  if not IsWebView2Installed then
  begin
    Log('WebView2 will be downloaded and installed');
  end;
end;

[Run]
; Install .NET 9 Desktop Runtime if not present
Filename: "https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe"; Parameters: "/install /quiet /norestart"; Description: "Installing .NET 9 Desktop Runtime"; Flags: shellexec runasoriginaluser waituntilterminated; Check: not IsDotNet9Installed

; Install WebView2 Runtime if not present  
Filename: "https://go.microsoft.com/fwlink/p/?LinkId=2124703"; Parameters: "/silent /install"; Description: "Installing Microsoft Edge WebView2 Runtime"; Flags: shellexec runasoriginaluser waituntilterminated; Check: not IsWebView2Installed

; Include common sections
#include "common.iss"
