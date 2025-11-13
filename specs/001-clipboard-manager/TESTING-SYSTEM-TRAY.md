# ClipMate System Tray Testing Guide

**Date**: 2025-11-12  
**Branch**: 001-clipboard-manager  
**Feature**: Phase 4 - System Tray Integration (T139-T147)  
**Status**: ✅ Exit bugs fixed (File→Exit + tray Exit notification)

## Running the Application

```powershell
cd Source/src/ClipMate.App
dotnet run
```

Or:

```powershell
cd Source
dotnet run --project src/ClipMate.App/ClipMate.App.csproj
```

## What to Test

### 1. Application Startup ✅

**Expected behavior**:
- App starts **without showing a window** (minimized to tray)
- System tray icon appears in notification area (Windows taskbar, right side)
- Icon shows as generic Windows application icon (temporary - see Resources/README.md)

**Command-line test**:
```powershell
# Start with window shown
dotnet run -- /show
# or
dotnet run -- --show
```

### 2. System Tray Icon ✅

**Test double-click**:
- Double-click the tray icon → Main window should appear
- Double-click again (or close window) → Window hides back to tray

**Test right-click context menu**:
- Right-click tray icon → Context menu appears
- Menu items:
  - **Show ClipMate** (bold) - Shows/activates main window
  - **Collections** - Submenu (dynamically loads from database)
  - **Exit** - Closes application completely

### 3. Collections Submenu ✅

**Test dynamic loading**:
- Hover over "Collections" in context menu
- Should see list of collections OR "(No collections)" if none exist
- If you have collections, they should appear with checkmark on active one

**Expected collections** (from DatabaseInitializationService):
- "Default" - The default collection created on first run

### 4. Window Minimize Behavior ✅

**Test standard close (X button)**:
- Show main window (double-click tray icon or click "Show ClipMate")
- Click the **X** button (top-right corner)
- Expected:
  - Window **hides** to tray (doesn't exit)
  - Balloon notification appears: "ClipMate is still running in the system tray..."
  - App continues running in background

**Test Shift+Close (force exit)**:
- Show main window
- Hold **Shift** key
- Click **X** button while holding Shift
- Expected:
  - Application **exits completely**
  - Tray icon disappears
  - No balloon notification

### 5. Exit from Tray Menu ✅

**Test proper exit**:
- Right-click tray icon
- Click "Exit"
- Expected:
  - Application **exits completely** (no balloon notification)
  - Tray icon disappears
  - All resources cleaned up

### 6. File → Exit Menu ✅

**Test menu exit**:
- Show main window (double-click tray icon)
- Click **File → Exit** in menu bar
- Expected:
  - Application **exits completely** (no balloon notification)
  - Tray icon disappears
  - Window closes without minimize-to-tray behavior

### 7. Clipboard Monitoring (Background) ✅

**Test clipboard capture while minimized**:
- Ensure app is running (tray icon visible)
- Copy some text (Ctrl+C from any app)
- Show main window (double-click tray)
- Expected:
  - New clip should appear in the clip list
  - Clipboard monitoring works even when window is hidden

## Known Issues / Expected Warnings

### Build Warnings (Non-critical)

1. **NU1510**: System.Drawing.Common package warning
   - Safe to ignore - package is needed for image clipboard support

### ~~Fixed Issues~~ ✅

1. ~~**File → Exit not working**~~ - **FIXED**
   - Added click handler to Exit menu item
   - Now properly closes application

2. ~~**Tray Exit showing "still running" balloon**~~ - **FIXED**
   - Added `_isExiting` flag to prevent balloon on intentional exit
   - Balloon only shows when minimizing via X button
   - Clean exit with File→Exit, Tray→Exit, or Shift+Close

See `BUGFIX-EXIT-BEHAVIOR.md` for details.

### Icon Placeholder

- **Current**: Using `SystemIcons.Application` (generic Windows icon)
- **Future**: Need to create proper ClipMate icon
- **See**: `Source/src/ClipMate.App/Resources/README.md` for requirements

## Logging Output

Check debug console for log messages:
```
ClipMate application started successfully
Database path: C:\Users\<You>\AppData\Local\ClipMate\clipmate.db
Database default data initialization complete
System tray initialized
Application started minimized to system tray
Clipboard monitoring coordinator started
```

When minimizing to tray:
```
MainWindow minimized to system tray
```

When exiting with Shift:
```
MainWindow closing with Shift key - allowing exit
```

## Database Location

ClipMate stores data in:
```
C:\Users\<YourUsername>\AppData\Local\ClipMate\clipmate.db
```

To reset database:
- Close ClipMate completely
- Delete the `clipmate.db` file
- Restart ClipMate (will recreate with default collection)

## Troubleshooting

### Tray icon doesn't appear
- Check Task Manager → ClipMate.App.exe is running
- Check notification area overflow (click ^ arrow in taskbar)
- Some Windows configurations hide tray icons by default

### Window won't show
- Try right-click tray icon → "Show ClipMate"
- Check if window is off-screen (Windows + arrow keys to move)

### Can't exit application
- Use tray menu → Exit
- Or use Shift+Close on main window
- Or kill process in Task Manager

### Multiple instances running
- **Not yet implemented**: Single instance enforcement (T149)
- Currently can launch multiple ClipMate.exe processes
- Each will have its own tray icon
- Fix coming soon

## Next Steps (Not Yet Implemented)

- [ ] T149: Single instance enforcement using Mutex
- [ ] T128-T129: ViewModel wiring (collection tree → clip list → preview)
- [ ] T111A-T111B: Unit tests for SystemTrayService
- [ ] Custom ClipMate icon design and integration
- [ ] Startup with Windows registry integration (T150)

## Success Criteria

System tray integration is **working** if:
- ✅ App starts to tray without window
- ✅ Double-click tray icon shows/hides window
- ✅ X button minimizes to tray (doesn't exit)
- ✅ Shift+X exits completely
- ✅ Tray menu shows "Show ClipMate" and "Exit"
- ✅ Collections submenu loads (even if empty)
- ✅ Clipboard monitoring works in background

---

**Report any issues or unexpected behavior!** 🐛
