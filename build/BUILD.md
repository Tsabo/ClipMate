# ClipMate Build System

This directory contains the Cake build system for ClipMate, which automates building, testing, and creating installers for the application.

## Prerequisites

### Required

- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Inno Setup 6+** - [Download](https://jrsoftware.org/isdl.php) or install via `choco install innosetup`
- **DevExpress License** - Required for building (commercial UI controls)

### Optional

- **PowerShell 7+** - Recommended for cross-platform support
- **Git** - For version calculation from tags

## Quick Start

### First-Time Setup

1. **Install .NET 9 SDK**
2. **Install Inno Setup** (add to PATH)
3. **Set DevExpress Feed Key:**

```powershell
# Windows PowerShell
$env:DEVEXPRESS_FEED_AUTH_KEY = "your-key-here"

# Or set permanently
[System.Environment]::SetEnvironmentVariable('DEVEXPRESS_FEED_AUTH_KEY', 'your-key-here', 'User')
```

4. **Restore .NET tools:**

```powershell
dotnet tool restore
```

### Building Locally

#### Build and Test

```powershell
.\build\build.ps1 -Target Build
.\build\build.ps1 -Target Test
```

#### Build Single-File Executable

```powershell
# Build a self-contained single .exe file (~200MB)
.\build\build.ps1 -Target Publish-SingleFile
```

The single-file executable will be located at:
```
build/publish-singlefile/Release/ClipMate.exe
```

This creates a fully self-contained executable with:
- All dependencies bundled
- No .NET runtime installation required
- Native libraries extracted on first run
- Compressed for smaller file size

#### Build Installer

```powershell
# Build the installer
.\build\build.ps1 -Target Build-Installer
```

#### Full Release Build

```powershell
.\build\build.ps1 -Target Release -Version "1.0.0"
```

This will:
- Clean previous builds
- Run all tests
- Build framework-dependent installer (~50MB + runtime downloads)
- Generate checksums
- Sanitize logs

**Note:** The installer will download .NET 9 Desktop Runtime and WebView2 at install time if not already present.

## Build Targets

| Target | Description |
|--------|-------------|
| `Clean` | Remove build artifacts |
| `Restore` | Restore NuGet packages |
| `Build` | Compile the solution |
| `Test` | Run all unit tests |
| `Publish` | Publish framework-dependent app |
| `Publish-SingleFile` | Publish self-contained single executable |
| `Build-Installer` | Create Windows installer (downloads runtimes at install time) |
| `Sign-Installer` | Sign installer (when SignPath configured) |
| `Sanitize-Logs` | Remove sensitive data from logs |
| `Package` | Generate checksums |
| `CI` | Full CI build (build + test) |
| `Release` | Full release build (everything) |

## Build Parameters

### --target (-t)

Specify the build target to execute:

```powershell
# PowerShell syntax (either works)
.\build\build.ps1 -Target Build
.\build\build.ps1 --target Build
```

### --version (-v)

Override auto-detected version:

```powershell
# PowerShell syntax (either works)
.\build\build.ps1 -Target Release -Version "1.2.3"
.\build\build.ps1 --target Release --version "1.2.3"
```

### --configuration (-c)

Set build configuration (Debug or Release):

```powershell
# PowerShell syntax (either works)
.\build\build.ps1 -Configuration Debug
.\build\build.ps1 --configuration Debug
```

### --verbosity

Set logging detail level (Quiet, Minimal, Normal, Verbose, Diagnostic):

```powershell
# PowerShell syntax (either works)
.\build\build.ps1 -Verbosity Diagnostic
.\build\build.ps1 --verbosity Diagnostic
```

## Version Management

ClipMate uses **MinVer** for automatic versioning from git tags.

### Version Calculation

- **Tagged commit** (`v1.0.0`): Version = `1.0.0`
- **Pre-release tag** (`v1.0.0-alpha.1`): Version = `1.0.0-alpha.1`
- **Untagged commit**: Version = `1.0.1-alpha.0.{height}+{sha}`

### Creating a Release

1. Ensure all changes are committed
2. Create and push a version tag:

```bash
git tag v1.0.0
git push origin v1.0.0
```

3. GitHub Actions will automatically build and create the release

### Manual Version Override

```powershell
.\build\build.ps1 --target Release --version "1.0.0-beta.1"
```

## DevExpress NuGet Feed

ClipMate uses DevExpress UI controls which require authentication.

### Getting Your Feed Key

1. Log in to [DevExpress.com](https://www.devexpress.com/)
2. Navigate to **Downloads** → **NuGet Feed**
3. Copy your personal feed URL (contains your auth key)
4. Extract the GUID between `nuget.devexpress.com/` and `/api`

### Setting the Environment Variable

The build system requires `DEVEXPRESS_FEED_AUTH_KEY` to be set:

```powershell
# Windows - Permanent
[Environment]::SetEnvironmentVariable('DEVEXPRESS_FEED_AUTH_KEY', 'YOUR-KEY-HERE', 'User')

# Windows - Session only
$env:DEVEXPRESS_FEED_AUTH_KEY = 'YOUR-KEY-HERE'

# Linux/macOS
export DEVEXPRESS_FEED_AUTH_KEY='YOUR-KEY-HERE'
```

### Feed URL Format

The build system constructs the full URL:
```
https://nuget.devexpress.com/{YOUR-KEY}/api/v3/index.json
```

## Output Locations

After building, artifacts are located in:

```
build/
├── publish/Release/                # Framework-dependent output
├── publish-singlefile/Release/     # Single-file executable
│   └── ClipMate.exe                # ~200MB self-contained exe
├── installer/output/               # Compiled installer
│   ├── ClipMate-Setup-{version}.exe
│   └── ClipMate-Setup-{version}.exe.sha256
└── logs/                           # Build logs and test results
```

## Installer Details

### Windows Installer (`ClipMate-Setup-{version}.exe`)

- **Size:** ~50MB
- **Target OS:** Windows 11 or Windows 10 version 1809+
- **Requirements:** Internet connection during install (only if dependencies missing)
- **Auto-downloads if needed:**
  - .NET 9 Desktop Runtime (~60-80MB)
  - WebView2 Evergreen Runtime (~150MB, usually already present on Windows 11)
- **Installation:** Standard Windows installer with Start Menu shortcuts, desktop icon option, uninstall support

## Troubleshooting

### "DEVEXPRESS_FEED_AUTH_KEY not set"

**Solution:** Set the environment variable as described above.

### "Could not find Inno Setup compiler (ISCC.exe)"

**Solution:** 
1. Install Inno Setup 6+
2. Add to PATH: `C:\Program Files (x86)\Inno Setup 6\`
3. Or install via Chocolatey: `choco install innosetup`

### ".NET 9 SDK not found"

**Solution:** Install .NET 9 SDK from https://dotnet.microsoft.com/download/dotnet/9.0

### "MinVer could not determine version"

**Cause:** Not in a git repository or no git tags

**Solution:** Use manual version override:
```powershell
.\build\build.ps1 --version "0.1.0-dev"
```

### Build fails with "Access denied" or permission errors

**Solution:** 
1. Close Visual Studio and any running ClipMate instances
2. Run `.\build\build.ps1 -Target Clean`
3. Try building again

## Advanced Topics

### Code Signing

See [SIGNPATH_SETUP.md](SIGNPATH_SETUP.md) for SignPath configuration when approved.

### CI/CD Integration

The build system is designed to work seamlessly with GitHub Actions. See `.github/workflows/` for CI/CD configuration.

### Customizing the Build

The main build script is `build.cake`. It uses Cake build system syntax. To customize:

1. Edit `build.cake`
2. Test locally before committing
3. See [Cake documentation](https://cakebuild.net/) for syntax reference

## Support

- **Issues:** https://github.com/Tsabo/ClipMate/issues
- **Discussions:** https://github.com/Tsabo/ClipMate/discussions
- **Documentation:** https://github.com/Tsabo/ClipMate/wiki
