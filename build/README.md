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

# Check startup / run the UI tests (other arguments go to the target)
.\build\build.ps1 -Target Smoke-Test -Configuration Debug --keep-running
.\build\build.ps1 -Target UI-Test -Configuration Debug --artifacts-path=C:\temp\artifacts

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
| `Sign-Binaries` | Sign the published ClipMate binaries and any unsigned third-party ones (only when `CERTUM_KEY_ID` is set; see [CODE_SIGNING.md](CODE_SIGNING.md)) |
| `Build-Installer` | Create Windows installer with InnoDependencyInstaller |
| `Verify-Signatures` | Fail if the app or installer is not validly signed (only when `CERTUM_KEY_ID` is set) |
| `Build-Font` | Build custom color font (ClipMate.ttf) from SVG icons |
| `Build-Docs` | Build Docusaurus documentation site |
| `Prepare-Worktree` | Copy the gitignored font and Monaco assets from the main checkout into a fresh git worktree |
| `Smoke-Test` | Start ClipMate against a temporary `--data-dir` and check it finishes startup (`--exe`, `--data-dir`, `--timeout`, `--keep-running`). It never captures your clipboard at startup |
| `UI-Probe` | Check a change by hand without touching your ClipMate: starts an isolated copy with `--setting section.key=value` applied, copies each `--copy` text and then each `--copy-html` fragment (as HTML plus its plain text, e.g. `--copy-html '<a href="https://example.com">Example</a>'`), presses each `--press` combination and saves a PNG of each of ClipMate's windows into `--capture <folder>` (only ClipMate's pixels, fit for a PR). Use hotkeys your own ClipMate doesn't hold, e.g. `--setting hotkeys.select_next="Ctrl+Alt+Shift+F9" --press Ctrl+Alt+Shift+F9`. Copying replaces your clipboard; text there is put back afterwards. Also `--wait-ms`, `--data-dir`, `--exe`, `--keep-running` |
| `UI-Test` | Run `Source/tests/ClipMate.UITests` (not in CI; uses the real clipboard; `--test-filter`). Prints the totals and each failure's first lines; the full output goes to `build/logs/ui-test-*.log` (`--full-output` streams it instead) |
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
  - ASP.NET Core 10 Runtime
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

- **Code Signing:** See [CODE_SIGNING.md](CODE_SIGNING.md)
- **CI/CD:** See `.github/workflows/`
- **Issues:** https://github.com/Tsabo/ClipMate/issues
