using System.Diagnostics;
using System.Windows.Automation;
using ClipMate.UITests.Fixtures;

namespace ClipMate.UITests.PageObjects;

/// <summary>
/// ClipMate's dialogs and collection tree, driven through UI Automation and the real mouse. UI Automation lists a dialog
/// under the window that owns it, and DevExpress's dialog buttons don't offer Invoke, so both are found by name anywhere
/// in ClipMate's process and clicked.
/// </summary>
public class WindowsPage(Process app)
{
    /// <summary>
    /// Clicks <paramref name="button" /> in the dialog titled <paramref name="title" />, once it is enabled.
    /// </summary>
    public async Task ClickDialogButtonAsync(string title, string button)
    {
        var dialog = await WaitForDialogAsync(title);
        var found = await Poll.UntilAsync(() => dialog.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.NameProperty, button))) is { Current.IsEnabled: true } enabled
                ? enabled
                : null,
            $"the {button} button of \"{title}\" to be enabled");

        Foreground.LeftClick(found.Current.BoundingRectangle);
    }

    /// <summary>
    /// Chooses a collection in the collection picker titled <paramref name="title" /> and clicks OK.
    /// </summary>
    public async Task PickCollectionAsync(string name, string title = "Select Collection")
    {
        // Tree items are named "Level <depth>: <name>"; the default database opens expanded
        var dialog = await WaitForDialogAsync(title);
        var collection = await Poll.UntilAsync(() => FindTreeItem(dialog, name), $"\"{name}\" in the \"{title}\" dialog");
        Foreground.LeftClick(collection.Current.BoundingRectangle);

        await ClickDialogButtonAsync(title, "OK");
    }

    /// <summary>
    /// Clicks the node named <paramref name="name" /> in the main window's collection tree.
    /// </summary>
    public async Task SelectTreeNodeAsync(string name)
    {
        var node = await Poll.UntilAsync(() => FindTreeItem(AutomationElement.RootElement, name), $"\"{name}\" in the collection tree");
        Foreground.LeftClick(node.Current.BoundingRectangle);
    }

    private AutomationElement? FindTreeItem(AutomationElement scope, string name) => scope
        .FindAll(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, app.Id),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)))
        .Cast<AutomationElement>()
        .FirstOrDefault(p => p.Current.Name.EndsWith($": {name}", StringComparison.Ordinal));

    private Task<AutomationElement> WaitForDialogAsync(string title) => Poll.UntilAsync(() => AutomationElement.RootElement.FindFirst(TreeScope.Descendants,
        new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, app.Id),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
            new PropertyCondition(AutomationElement.NameProperty, title))), $"the \"{title}\" dialog");
}
