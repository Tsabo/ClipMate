# ClipMate UI Tests

This project contains UI automation tests for ClipMate using [WPF Pilot](https://github.com/WPF-Pilot/WpfPilot).

## About WPF Pilot

WPF Pilot is a robust UI automation framework for WPF applications that provides:
- Reliable element location strategies
- Clean, maintainable test code
- Integration with popular test frameworks (xUnit, NUnit, TUnit)
- Support for DevExpress controls

## Documentation

- [WPF Pilot GitHub](https://github.com/WPF-Pilot/WpfPilot)
- [WPF Pilot Wiki](https://github.com/WPF-Pilot/WpfPilot/wiki)
- [Getting Started Guide](https://github.com/WPF-Pilot/WpfPilot/wiki/Getting-Started)

## Running Tests

**Important**: These tests are excluded from CI/CD (the project is not in `ClipMate.sln`) and should be run on-demand only.

Each test starts its own ClipMate (the `ClipMate.App.exe` built beside the tests) with `--data-dir` pointing at a temporary folder, so your settings and databases are never touched. Tests that exercise the clipboard use the real, shared Windows clipboard: another clipboard manager you have running will see their clips. They put the clipboard's previous content back afterwards.

### From Command Line
```powershell
.\build\build.ps1 -Target UI-Test -Configuration Debug
.\build\build.ps1 -Target UI-Test -Configuration Debug --test-filter=/*/*/ClipboardFileDropRoundTripTests/*
```

Add `--artifacts-path=<folder>` when a running ClipMate locks `bin`.

### From Visual Studio
- Open Test Explorer
- Select the ClipMate.UITests project
- Run selected tests

## Page Object Pattern

This test suite uses the **Page Object Pattern** to create maintainable, reusable test code. Each window or major UI component has a corresponding "Page" class that encapsulates element location and interaction logic.

### Benefits
- **Maintainability**: When UI changes, update only the page object, not every test
- **Reusability**: Common interactions (e.g., selecting a collection) are defined once
- **Readability**: Tests read like user scenarios, not technical element queries

### Structure

```
ClipMate.UITests/
├── PageObjects/                  # Page classes representing UI windows/components
│   ├── ClipListPage.cs           # Clip list via UI Automation: find, wait for and select clips
│   └── CollectionTreePage.cs     # Collection tree via WPF Pilot
├── Tests/
│   ├── AutoTargetingTests.cs                # Runs: switch between two windows, open ClipMate, QuickPaste into the last one
│   ├── CaptureTimingTests.cs                # Runs: formats written one at a time are captured once; a slow source doesn't freeze the window
│   ├── ClipboardFileDropRoundTripTests.cs   # Runs: capture an Explorer cut, pick it, paste it with the shell
│   ├── PowerPasteTests.cs                   # Runs: paste detection, and the clipboard after Power Paste ends
│   └── BounceTrackingTests.cs, DragDropTests.cs, ExampleTests.cs, RetentionPolicyTests.cs   # Skipped placeholders
└── Fixtures/
    ├── AppFixture.cs             # Starts an isolated ClipMate (--data-dir) and waits for startup
    ├── ClipboardTestProgram.cs   # Another program that copies in steps or renders its data slowly
    ├── Foreground.cs             # Switches the foreground window and presses keys, as a user would
    ├── ForegroundTestWindow.cs   # A test window in the foreground, to paste as the user's application
    ├── Poll.cs                   # Waits on another process until a deadline
    ├── SystemClipboard.cs        # STA clipboard access, backup/restore, raw format names
    ├── TestAppWindow.cs          # A window in another process to paste into, with its text read back
    └── TestProgramProfile.cs     # The test program's Application Profile, written before ClipMate starts
```

## Writing New Tests

1. **Create/Update Page Object** - Encapsulate element location and interaction
2. **Write Test** - Use page object methods to perform user actions
3. **Assert** - Verify expected behavior

`ClipboardFileDropRoundTripTests` is the working reference. A test that picks a clip:

```csharp
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
public class PickClipTests(AppFixture appFixture)
{
    [Test]
    public async Task PickingAClip_PutsItOnTheClipboard()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);

            await clipList.SelectAsync("Welcome to ClipMate");

            // Wait for the picked clip's own text: until ClipMate copies it, the clipboard still holds whatever was there
            await Poll.UntilAsync(() => SystemClipboard.OnSta(() => Clipboard.GetText().Contains("This is a sample clip")),
                "the sample clip on the clipboard");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }
}
```

`ClipListPage` documents how rows behave: titles are truncated (match long content by its icon), the grid recreates
rows when a clip arrives (`SelectAsync` looks the row up afresh), and ClipMate copies a newly captured clip back by
itself, which can overwrite what a test just put on the clipboard.

### Copying as another program

- Copy with `ForegroundTestWindow.Copy` and keep the window open until the test ends. A copy made on
  `SystemClipboard.OnSta`'s short-lived thread has no clipboard owner by the time ClipMate reads it, so ClipMate takes
  the Source Application from whatever window is in the foreground. When a copying thread ends, OLE also puts the copy
  on the clipboard again, which ClipMate captures as a new change.
- The test program gets an Application Profile on its first copy, with ClipMate 7's defaults: images (when there is
  text), file lists and private formats start off. `TestProgramProfile.Write` switches formats on before ClipMate starts.

### Remote Desktop

In a Remote Desktop session the client (`msrdc`) reads the clipboard on every change, opening it several times. Tests
that count clipboard reads, such as Power Paste's paste detection, see these as background reads.

## Troubleshooting

### Test Hangs
- Ensure application window is visible (not minimized)
- Check for modal dialogs blocking interaction

### Element Not Found
- Verify element exists in current UI state
- Check AutomationProperties.AutomationId or Name is set correctly
- Use WPF Pilot's diagnostic tools to inspect element tree

### Tests Pass Locally but Fail in Different Environment
- UI tests are environment-sensitive
- This is why they're excluded from CI/CD
- Run manually on representative test environments
