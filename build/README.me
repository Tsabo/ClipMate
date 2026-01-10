# ClipMate Build System

This directory contains the Cake build system for ClipMate.

## Prerequisites

- **.NET 10 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Inno Setup 6+** - [Download](https://jrsoftware.org/isdl.php) or `choco install innosetup`
- **DevExpress License** - Required for building (commercial UI controls)
- **PowerShell 7+** - Recommended

## Quick Start

### DevExpress Feed Setup

Set your DevExpress NuGet feed authentication key:

```powershell
# Get your key from https://www.devexpress.com/ → Downloads → NuGet Feed
$env:DEVEXPRESS_FEED_AUTH_KEY = "your-key-here"

# Or set permanently:
[Environment]::SetEnvironmentVariable('DEVEXPRESS_FEED_AUTH_KEY', 'your-key-here', 'User')
```

### Common Build Commands

```powershell
# Build and test
.\build\build.ps1 -Target Build
.\build\build.ps1 -Target Test

# Create installer
.\build\build.ps1 -Target Build-Installer

# Full release build
.\build\build.ps1 -Target Release
```

## Build Targets

| Target | Description |
|--------|-------------|
| `Build` | Compile the solution |
| `Test` | Run all unit tests |
| `Publish` | Publish framework-dependent app |
| `Publish-SingleFile` | Create self-contained single executable (~200MB) |
| `Build-Installer` | Create Windows installer with InnoDependencyInstaller |
| `Build-Font` | Build custom color font (ClipMate.ttf) from SVG icons |
| `Build-Docs` | Build Docusaurus documentation site |
| `CI` | Full CI build (build + test) |
| `Release` | Full release build with installer |

## Versioning

ClipMate uses **MinVer** for automatic versioning from git tags:

- Tagged commit (`v1.0.0`): Version = `1.0.0`
- Untagged commit: Version = `1.0.1-alpha.0.{height}+{sha}`

**Create a release:**
```bash
git tag v1.0.0
git push origin v1.0.0
```

**Manual override:**
```powershell
.\build\build.ps1 --target Release --version "1.0.0-beta.1"
```

## Output Locations

```
build/
├── publish/Release/                # Framework-dependent output
├── publish-singlefile/Release/     # Single-file executable (~200MB)
│   └── ClipMate.exe
├── installer/output/               # Installer
│   └── ClipMate-Setup-{version}.exe
└── logs/                           # Build logs
```

## Installer Details

- **Size:** ~50MB
- **Target:** Windows 10 1809+ / Windows 11
- **Auto-installs if missing:**
  - .NET 10 Desktop Runtime
  - WebView2 Runtime (via InnoDependencyInstaller)

## Troubleshooting

**"DEVEXPRESS_FEED_AUTH_KEY not set"**
→ Set environment variable as shown above

**"Could not find Inno Setup compiler"**
→ Install Inno Setup and add to PATH

**".NET 10 SDK not found"**
→ Install from https://dotnet.microsoft.com/download/dotnet/10.0

**Build permission errors**
→ Close Visual Studio/ClipMate, run `.\build\build.ps1 -Target Clean`

## Additional Resources

- **Code Signing:** See [SIGNPATH_SETUP.md](SIGNPATH_SETUP.md)
- **CI/CD:** See `.github/workflows/`
- **Issues:** https://github.com/Tsabo/ClipMate/issues
