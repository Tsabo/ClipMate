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

**Important**: These tests are excluded from CI/CD and should be run on-demand only.

### From Visual Studio
- Open Test Explorer
- Select the ClipMate.UITests project
- Run selected tests

### From Command Line
```powershell
dotnet test Source/tests/ClipMate.UITests/ClipMate.UITests.csproj
```

## Page Object Pattern

This test suite uses the **Page Object Pattern** to create maintainable, reusable test code. Each window or major UI component has a corresponding "Page" class that encapsulates element location and interaction logic.

### Benefits
- **Maintainability**: When UI changes, update only the page object, not every test
- **Reusability**: Common interactions (e.g., selecting a collection) are defined once
- **Readability**: Tests read like user scenarios, not technical element queries

### Structure

```
ClipMate.UITests/
├── PageObjects/              # Page classes representing UI windows/components
│   ├── CollectionTreePage.cs
│   ├── ClipListPage.cs
│   └── OptionsDialogPage.cs
├── Tests/                    # Actual test classes
│   ├── CollectionTreeTests.cs
│   └── DragDropTests.cs
└── Fixtures/                 # Shared setup/teardown logic
    └── AppFixture.cs
```

## Example Tests

See the example tests in this project for reference:
- `Tests/ExampleTests.cs` - Basic button clicks and tree selection
- `PageObjects/CollectionTreePage.cs` - Window-level page object implementation

## Writing New Tests

1. **Create/Update Page Object** - Encapsulate element location and interaction
2. **Write Test** - Use page object methods to perform user actions
3. **Assert** - Verify expected behavior

Example:
```csharp
[Test]
public async Task CanSelectCollection()
{
    var treePage = new CollectionTreePage(appWindow);
    await treePage.SelectCollectionAsync("InBox");
    
    await Assert.That(treePage.SelectedCollectionName)
        .IsEqualTo("InBox");
}
```

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
