# First-Run Setup Wizard - Implementation Complete

## Problem

The application was crashing on first run because:
1. MainWindow loaded **before** database was created
2. UI tried to query non-existent tables
3. Hundreds of exceptions flooded the log
4. No way for users to choose database location

## Solution

Added a **pre-UI setup wizard** that runs before the main application loads.

## Architecture

```
App.OnStartup()
    ↓
CheckDatabaseAndRunSetupIfNeededAsync()
    ├─ Database exists & valid? → Skip wizard
    └─ Database missing/invalid? → Show SetupWizard
        ├─ User chooses location
        ├─ Create schema (migrations)
        ├─ Seed default data
        └─ Return database path
    ↓
CreateHostBuilder(databasePath)
    ↓
Start hosted services (with confirmed database)
    ↓
MainWindow loads successfully ✅
```

## Components Added

### 1. SetupWizard.xaml
**File:** `src/ClipMate.App/Views/SetupWizard.xaml`

Professional setup dialog with:
- **Header** - Welcome message
- **Database location picker** - Browse button + path textbox
- **Info panel** - Explains what gets stored
- **Features list** - Checkmarks showing enabled features
- **Progress overlay** - Indeterminate progress bar during setup
- **Continue/Cancel buttons**

**Design:**
- Material Design inspired (Blue header, white content)
- Non-resizable, centered
- Clear visual hierarchy
- Responsive layout

### 2. SetupWizard.xaml.cs
**File:** `src/ClipMate.App/Views/SetupWizard.xaml.cs`

**Key Methods:**

```csharp
public async Task ContinueButton_Click()
{
    // 1. Validate path
    // 2. Show progress overlay
    // 3. Create database directory
    // 4. Create DbContext
    // 5. Apply migrations
    // 6. Seed default data
    // 7. Close wizard with success
}
```

**Error Handling:**
- Invalid paths → Warning message
- Setup failure → Detailed error message
- Allow retry (re-enable buttons)

### 3. App.xaml.cs Updates
**File:** `src/ClipMate.App/App.xaml.cs`

**New Method:**
```csharp
private async Task<bool> CheckDatabaseAndRunSetupIfNeededAsync()
{
    // 1. Check if database file exists
    // 2. Validate database has required tables
    // 3. If invalid/missing → Show SetupWizard
    // 4. Return chosen database path
}
```

**Changes to OnStartup():**
```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    // ...single instance check
    
    // NEW: Run setup wizard if needed
    if (!await CheckDatabaseAndRunSetupIfNeededAsync())
    {
        Shutdown(0); // User cancelled
        return;
    }
    
    // Build host with confirmed database path
    _host = CreateHostBuilder(_databasePath!).Build();
    
    // Start hosted services
    await _host.StartAsync();
}
```

## Flow Diagram

### First Run (No Database)
```
Launch App
    ↓
OnStartup()
    ↓
CheckDatabaseAndRunSetupIfNeededAsync()
    ├─ Database file exists? → NO
    └─ Show SetupWizard
        ├─ User chooses: %LOCALAPPDATA%\ClipMate\clipmate.db
        ├─ Or custom location via Browse
        ↓
        CreateAsync Database Schema
        ├─ Apply migrations
        └─ Seed 13 default collections
        ↓
        SetupCompleted = true
        ↓
Return database path
    ↓
CreateHostBuilder(databasePath)
    ↓
_host.StartAsync()
    ├─ DatabaseInitializationHostedService (skipped - already initialized)
    ├─ ClipboardCoordinator (starts monitoring)
    └─ PowerPasteCoordinator (starts listening)
    ↓
MainWindow.Show()
    ↓
✅ UI loads successfully with data
```

### Subsequent Runs (Database Exists)
```
Launch App
    ↓
CheckDatabaseAndRunSetupIfNeededAsync()
    ├─ Database file exists? → YES
    ├─ Validate tables exist → YES
    └─ Return existing path
    ↓
CreateHostBuilder(existingPath)
    ↓
Start normally ✅
```

### User Cancels Setup
```
Launch App
    ↓
SetupWizard.ShowDialog()
    ├─ User clicks Cancel
    └─ SetupCompleted = false
    ↓
Return false
    ↓
Shutdown(0)
    ↓
App exits gracefully
```

## Setup Wizard Features

### Database Location
- **Default:** `%LOCALAPPDATA%\ClipMate\clipmate.db`
- **Custom:** Browse button opens SaveFileDialog
- **Validation:** Checks directory exists/writable

### Progress Feedback
```
Creating database schema...
    ↓ (migrations applied)
Seeding default data...
    ↓ (13 collections created)
Setup complete! ✅
```

### Error Recovery
If setup fails:
1. Hide progress overlay
2. Re-enable buttons
3. Show error message with details
4. User can:
   - Try again (same location)
   - Choose different location
   - Cancel (exit app)

## Default Data Seeded

The setup wizard creates the ClipMate 7.5 collection structure:

**Root Collections (5):**
1. **Inbox** - Default destination for new clips
2. **Safe** - Important clips folder
3. **Overflow** - When Inbox is full
4. **Samples** - Sample clips
5. **Virtual** - Parent for smart collections

**Virtual Collections (8):**
1. **Today** - Clips from today
2. **This Week** - Last 7 days
3. **This Month** - Last 30 days
4. **Everything** - All clips
5. **Since Last Import** - Import tracking
6. **Since Last Export** - Export tracking
7. **Bitmaps** - Image clips only
8. **Keystroke Macros** - Macro clips

**Total:** 13 collections created automatically

## Benefits

### 1. No More First-Run Crashes ✅
- Database created **before** UI loads
- No exceptions from missing tables
- Graceful setup experience

### 2. User Control ✅
- Choose database location
- Portable databases (USB drive, cloud folder)
- Multi-profile support (different databases)

### 3. Professional UX ✅
- Clear welcome message
- Visual progress feedback
- Helpful info panels
- Error recovery

### 4. Developer Friendly ✅
- Easy to test first-run scenarios
- Clean separation of concerns
- Proper async/await patterns
- Comprehensive error handling

## Database Location Options

### Default (Recommended)
```
C:\Users\<Username>\AppData\Local\ClipMate\clipmate.db
```

**Pros:**
- Standard Windows location
- Per-user isolation
- Automatic cleanup on uninstall

### Custom Locations

**USB Drive:**
```
E:\ClipMate\clipmate.db
```

**Pros:**
- Portable between PCs
- Take clips anywhere

**Cloud Folder:**
```
C:\Users\<Username>\Dropbox\ClipMate\clipmate.db
```

**Pros:**
- Sync across devices
- Automatic backup

**⚠ Warning:** SQLite doesn't handle concurrent writes from multiple devices well. Use with caution.

## Testing Scenarios

### Scenario 1: Fresh Install
```
1. Delete %LOCALAPPDATA%\ClipMate\
2. Launch app
3. Verify SetupWizard appears
4. Click Continue with default path
5. Verify database created
6. Verify MainWindow loads with collections
```

### Scenario 2: Custom Location
```
1. Launch first time
2. SetupWizard appears
3. Click Browse
4. Choose D:\MyClips\clipmate.db
5. Click Continue
6. Verify database created at custom location
7. App uses custom database
```

### Scenario 3: Invalid Location
```
1. Launch first time
2. Try to save to C:\Windows\clipmate.db (no permission)
3. Setup fails with error
4. Buttons re-enabled
5. User chooses valid location
6. Setup succeeds
```

### Scenario 4: Cancel Setup
```
1. Launch first time
2. SetupWizard appears
3. Click Cancel
4. App exits gracefully (no crash)
```

### Scenario 5: Corrupted Database
```
1. Create empty file at %LOCALAPPDATA%\ClipMate\clipmate.db
2. Launch app
3. Database validation fails
4. SetupWizard appears
5. User confirms location
6. Database recreated properly
```

## Code Quality

### Error Handling
```csharp
try
{
    await context.Database.MigrateAsync();
    await seeder.SeedDefaultDataAsync();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to set up database");
    
    // Re-enable UI
    ProgressOverlay.Visibility = Visibility.Collapsed;
    ContinueButton.IsEnabled = true;
    
    // Show user-friendly error
    MessageBox.Show($"Failed: {ex.Message}");
}
```

### Async/Await
All database operations are properly async:
- `MigrateAsync()`
- `SeedDefaultDataAsync()`
- `AnyAsync()` for validation

### Logging
Full logging trail:
```
INFO: Starting database setup at: C:\Users\...\clipmate.db
INFO: Creating directory: C:\Users\...\ClipMate
INFO: Applying database migrations...
INFO: Seeding default collections...
INFO: Created 5 root collections
INFO: Created 8 virtual collections
INFO: Database setup completed successfully
```

### User Feedback
Every step visible to user:
1. "Creating database schema..."
2. "Seeding default data..."
3. Success → Dialog closes
4. Failure → Clear error message

## Future Enhancements

### Potential Additions
1. **Database import** - Import existing ClipMate 7.5 database
2. **Multiple profiles** - Choose from saved locations
3. **Backup reminder** - Suggest backup location
4. **Size estimate** - Show expected database size
5. **Advanced options** - WAL mode, cache size, etc.

### Migration Path
For users upgrading from ClipMate 7.5:
```
1. Copy clipmate.mdb to new location
2. SetupWizard offers "Import existing database"
3. Convert Access DB → SQLite
4. Run migrations to update schema
5. Continue with new version
```

## Summary

✅ **First-run crashes eliminated**
- Setup wizard runs before UI
- Database validated before loading
- No exceptions from missing tables

✅ **User control over database location**
- Default location provided
- Browse for custom location
- Support for portable/cloud databases

✅ **Professional setup experience**
- Clean UI with progress feedback
- Clear instructions
- Error recovery

✅ **Ready for production**
- Comprehensive error handling
- Full logging
- Tested scenarios

The application now has a **proper first-run experience** that matches professional software standards!
