# ClipMate Configuration System

## Overview

ClipMate uses a TOML-based configuration system that allows end users to customize application behavior, manage multiple databases, configure hotkeys, and control clipboard format capture per application.

## Configuration File Location

The configuration file is located at:
```
%LOCALAPPDATA%\ClipMate\clipmate.toml
```

For example: `C:\Users\YourUsername\AppData\Local\ClipMate\clipmate.toml`

## Configuration Structure

### Version
```toml
Version = 1
```
Configuration file format version. Used for future migrations.

### Default Database
```toml
DefaultDatabase = "MyClips"
```
The database that loads automatically on startup.

### Preferences Section

Controls general application behavior:

```toml
[Preferences]
AutoCaptureAtStartup = true          # Start capturing clipboard on startup
HideTaskbarIcon = true               # Hide from Windows taskbar
ShowSystemTrayIcon = true            # Show in system tray
CaptureExistingClipboard = true      # Capture clipboard content at startup
DelayAfterCopy = 999                 # Delay (ms) after copy operation
DelayOnClipboardUpdate = 250         # Delay (ms) on clipboard change
BeepOnUpdate = true                  # Sound notification on clipboard update
BeepOnAppend = true                  # Sound on append operation
BeepOnErase = true                   # Sound on erase operation
BeepOnFilter = true                  # Sound on filter match
BeepOnIgnore = true                  # Sound on ignored clipboard change
ShowHint = true                      # Show tooltip hints
HintHidePause = 4500                 # Tooltip display duration (ms)
Language = "English"                 # UI language
LogLevel = 3                         # 0=None, 1=Error, 2=Warning, 3=Info, 4=Debug, 5=Verbose
IsRegistered = false                 # License status
PowerPasteDelay = 100                # PowerPaste popup delay (ms)
PowerPasteShield = true              # Prevent accidental PowerPaste activation
PowerPasteDelimiter = ",.;:\\n\\t"   # Delimiters for PowerPaste split
PowerPasteTrim = true                # Trim whitespace in PowerPaste
PowerPasteIncludeDelimiter = false   # Include delimiter in pasted text
PowerPasteLoop = false               # Loop through PowerPaste items
PowerPasteExplode = false            # Split clipboard into multiple items
```

### Hotkeys Section

Configure global keyboard shortcuts:

```toml
[Hotkeys]
Activate = "Ctrl+Alt+C"              # Show main window
Capture = "Win+C"                    # Manual clipboard capture
AutoCapture = "Win+Shift+C"          # Toggle auto-capture
QuickPaste = "Ctrl+Shift+V"          # PowerPaste popup
ScreenCapture = "Ctrl+Alt+F12"       # Screen capture
ScreenCaptureObject = "Ctrl+Alt+F11" # Capture screen object
SelectNext = "Ctrl+Alt+N"            # Select next clip
SelectPrevious = "Ctrl+Alt+P"        # Select previous clip
ViewClip = "Ctrl+Alt+F2"             # View clip details
```

**Hotkey Format:** Modifiers + Key separated by `+`
- **Modifiers:** `Ctrl`, `Alt`, `Shift`, `Win`
- **Keys:** Letter keys (`A`-`Z`), function keys (`F1`-`F12`), or special keys
- **Examples:** `Ctrl+Shift+V`, `Win+C`, `Alt+F4`

### Database Configuration

Define multiple clipboard databases. Each database is stored in a separate file and can have different settings.

```toml
[Databases.MyClips]
Name = "My Clips"                                           # Display name
Directory = "C:\\Users\\YourUsername\\AppData\\Local\\ClipMate"  # Storage location
AutoLoad = true                                             # Load on startup
AllowBackup = true                                          # Enable backups
ReadOnly = false                                            # Read-only mode
CleanupMethod = 3                                           # 0=None, 1=Manual, 2=OnExit, 3=Daily, 4=Weekly
PurgeDays = 7                                               # Delete clips older than N days
UserName = "YourUsername"                                   # Associated user
IsRemote = false                                            # Is this a remote database?
MultiUser = false                                           # Allow multi-user access
IsCommandLineDatabase = false                               # Created via command-line
UseModificationTimeStamp = true                             # Track modification time
```

**Adding a Second Database:**
```toml
[Databases.WorkClips]
Name = "Work Clips"
Directory = "C:\\Users\\YourUsername\\Documents\\ClipMate\\Work"
AutoLoad = false
AllowBackup = true
ReadOnly = false
CleanupMethod = 3
PurgeDays = 30
UserName = "YourUsername"
IsRemote = false
MultiUser = false
```

### Application Profiles

Control which clipboard formats are captured from specific applications. This prevents unwanted formats and reduces storage.

```toml
[ApplicationProfiles.DEVENV]
ApplicationName = "DEVENV"  # Visual Studio process name
Enabled = true

[ApplicationProfiles.DEVENV.Formats]
TEXT = 1                    # Capture plain text (1=capture, 0=ignore)
"Rich Text Format" = 1      # Capture RTF
"HTML Format" = 0           # Ignore HTML
DataObject = 0              # Ignore DataObject format
```

**Common Applications:**
- `DEVENV` - Visual Studio
- `CODE` - VS Code
- `MSEDGE` - Microsoft Edge
- `CHROME` - Google Chrome
- `NOTEPAD++` - Notepad++
- `WINWORD` - Microsoft Word
- `EXCEL` - Microsoft Excel

**Common Clipboard Formats:**
- `TEXT` - Plain text
- `UNICODETEXT` - Unicode text
- `Rich Text Format` - RTF formatted text
- `HTML Format` - HTML markup
- `DataObject` - .NET DataObject
- `HDROP` - File drop list

Application profiles are automatically created when ClipMate detects clipboard operations from new applications.

## Programmatic Usage

### Accessing Configuration Service

```csharp
using ClipMate.Core.Services;
using Microsoft.Extensions.DependencyInjection;

// Get configuration service from DI
var configService = serviceProvider.GetRequiredService<IConfigurationService>();

// Load configuration
var config = await configService.LoadAsync();

// Access settings
var logLevel = config.Preferences.LogLevel;
var databases = config.Databases;
```

### Modifying Configuration

```csharp
// Add a new database
var newDatabase = new DatabaseConfiguration
{
    Name = "Project Clips",
    Directory = @"C:\Projects\ClipMate",
    AutoLoad = false,
    PurgeDays = 30
};
await configService.AddOrUpdateDatabaseAsync("ProjectClips", newDatabase);

// Update preferences
configService.Configuration.Preferences.LogLevel = 4;
await configService.SaveAsync();

// Add application profile
var profile = new ApplicationProfile
{
    ApplicationName = "NOTEPAD",
    Enabled = true,
    Formats = new Dictionary<string, int>
    {
        { "TEXT", 1 },
        { "UNICODETEXT", 1 }
    }
};
await configService.AddOrUpdateApplicationProfileAsync("NOTEPAD", profile);
```

### Reset to Defaults

```csharp
await configService.ResetToDefaultsAsync();
```

## Manual Editing

You can manually edit `clipmate.toml` when ClipMate is not running:

1. Close ClipMate completely (exit from system tray)
2. Open `%LOCALAPPDATA%\ClipMate\clipmate.toml` in a text editor
3. Make your changes following TOML syntax
4. Save the file
5. Restart ClipMate

**Backup:** ClipMate automatically creates `.bak` backup files when saving configuration.

## Troubleshooting

### Configuration Not Loading
- Check file permissions on `%LOCALAPPDATA%\ClipMate\`
- Verify TOML syntax is valid (use an online TOML validator)
- Check application logs for errors
- Delete `clipmate.toml` to regenerate default configuration

### Hotkeys Not Working
- Verify hotkey format is correct (`Ctrl+Shift+V` not `Ctrl-Shift-V`)
- Check for conflicts with other applications
- Try different key combinations
- Restart ClipMate after changing hotkeys

### Database Not Found
- Verify `Directory` path exists
- Check file permissions
- Ensure database file name matches directory structure
- Database files use `.db` extension

## Migration from ClipMate 7.5

ClipMate 7.5 stored configuration in the Windows Registry. ClipMate uses TOML files for:

**Advantages:**
- ✅ Portable - easily backup and share configurations
- ✅ Version control friendly - track changes in git
- ✅ Human readable - edit with any text editor
- ✅ Cross-platform ready - no Windows Registry dependency
- ✅ Safer - no risk of registry corruption

**Registry Keys (Legacy):**
- `HKEY_CURRENT_USER\Software\Thornsoft\ClipMate7\Preferences`
- `HKEY_CURRENT_USER\Software\Thornsoft\ClipMate7\HotKeys`
- `HKEY_CURRENT_USER\Software\Thornsoft\ClipMate7\Databases`
- `HKEY_CURRENT_USER\Software\Thornsoft\ClipMate7\Application Profile`

These are **not** used by the new ClipMate.

## Example Configuration

See `Configuration/clipmate.example.toml` for a complete example with comments.
