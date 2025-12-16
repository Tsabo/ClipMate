using WpfPilot;

namespace ClipMate.UITests.PageObjects;

/// <summary>
/// Page object representing the Collection Tree view in ClipMate.
/// Encapsulates interactions with the collection tree control.
/// NOTE: This is a simplified implementation. Full tree node inspection
/// requires more complex WPF Pilot usage with Invoke() methods.
/// </summary>
public class CollectionTreePage
{
    private readonly Element _mainWindow;
    private readonly AppDriver _appDriver;

    public CollectionTreePage(Element mainWindow, AppDriver appDriver)
    {
        _mainWindow = mainWindow;
        _appDriver = appDriver;
    }

    /// <summary>
    /// Gets the TreeViewControl element.
    /// </summary>
    private Element GetTreeView()
    {
        return _appDriver.GetElement(x => x["Name"] == "TreeView");
    }

    /// <summary>
    /// Simulates pressing the + key to move collection up.
    /// </summary>
    public async Task PressMovUpKeyAsync()
    {
        _appDriver.Keyboard.Type("+");
        await Task.Delay(100);
    }

    /// <summary>
    /// Simulates pressing the - key to move collection down.
    /// </summary>
    public async Task PressMovDownKeyAsync()
    {
        _appDriver.Keyboard.Type("-");
        await Task.Delay(100);
    }

    /// <summary>
    /// Verifies the tree view exists and is visible.
    /// </summary>
    public bool IsTreeViewVisible()
    {
        try
        {
            var tree = GetTreeView();
            return tree != null;
        }
        catch
        {
            return false;
        }
    }
}
