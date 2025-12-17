using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using TUnit.Core;

namespace ClipMate.UITests.Tests;

/// <summary>
/// UI tests for collection tree functionality including drag-drop, visual styling, and keyboard shortcuts.
/// NOTE: These tests demonstrate the WPF test infrastructure. Full implementation requires:
/// - Setting up test data (collections, clips)
/// - More complex WPF Pilot Invoke() calls for deep property access
/// - DevExpress TreeViewControl API understanding
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public class CollectionTreeTests(AppFixture appFixture)
{
    private readonly AppFixture _appFixture = appFixture;

    [Before(Test)]
    public async Task Setup()
    {
        await _appFixture.StartAppAsync();
    }

    /// <summary>
    /// Verifies that the collection tree view loads and is visible.
    /// </summary>
    [Test]
    [Skip("Disabled temporarily - mutex prevents multiple instances, needs investigation")]
    public async Task CollectionTree_Loads_Successfully()
    {
        var mainWindow = _appFixture.GetMainWindow();
        var treePage = new CollectionTreePage(mainWindow, _appFixture.App);

        var isVisible = treePage.IsTreeViewVisible();

        await Assert.That(isVisible).IsTrue();
    }

    /// <summary>
    /// Verifies the tree view exists in the main window.
    /// </summary>
    [Test]
    [Skip("Disabled temporarily - mutex prevents multiple instances, needs investigation")]
    public async Task TreeView_Element_Exists()
    {
        var treeView = _appFixture.App.GetElement(x => x["Name"] == "TreeView");

        await Assert.That(treeView).IsNotNull();
        await Assert.That(treeView.TypeName).IsEqualTo("TreeViewControl");
    }

    /// <summary>
    /// Placeholder for visual styling tests.
    /// Full implementation requires test data setup and complex property access.
    /// </summary>
    [Test]
    [Skip("Requires test data setup and complex WPF Pilot property access")]
    public async Task ReadOnlyCollections_DisplayInRed()
    {
        // TODO: Requires:
        // 1. Test database with known collections
        // 2. WPF Pilot Invoke() to access nested properties
        // 3. Visual tree traversal to find TextBlock elements
        await Task.CompletedTask;
    }

    /// <summary>
    /// Placeholder for keyboard shortcut tests.
    /// Full implementation requires manual sort mode and test data.
    /// </summary>
    [Test]
    [Skip("Requires manual sort mode and test data setup")]
    public async Task MoveUp_Keyboard_Works()
    {
        var mainWindow = _appFixture.GetMainWindow();
        var treePage = new CollectionTreePage(mainWindow, _appFixture.App);

        // This demonstrates the API - actual test needs collection setup
        await treePage.PressMovUpKeyAsync();
        
        // Would verify collection moved up
        await Task.CompletedTask;
    }
}


