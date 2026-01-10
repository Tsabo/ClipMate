; ClipMate Framework-Dependent Installer Script
; Requires .NET 10 Desktop Runtime and WebView2 (downloaded automatically via InnoDependencyInstaller)

; Include InnoDependencyInstaller for automatic dependency management
#include "CodeDependencies.iss"

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
AppCopyright=Copyright © 2026 {#MyAppPublisher}

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
WelcomeLabel2=This will install [name/ver] on your computer.%n%nInstaller size: ~50MB%n%nThis installer will automatically download and install required dependencies:%n• .NET 10 Desktop Runtime (if not installed)%n• Microsoft Edge WebView2 Runtime (if not installed)%n%nAn internet connection is required for dependency installation.%n%nIt is recommended that you close all other applications before continuing.

[Code]
function InitializeSetup(): Boolean;
begin
  // Add .NET 10 Desktop Runtime dependency
  Dependency_AddDotNet100Desktop;
  
  // Add WebView2 Runtime dependency
  Dependency_AddWebView2;
  
  Result := True;
end;

[Run]
; Include common sections (Files, Icons, Registry, Tasks, Run)
#include "common.iss"
