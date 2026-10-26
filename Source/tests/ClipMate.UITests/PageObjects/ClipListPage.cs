using System.Diagnostics;
using System.Windows.Automation;
using ClipMate.UITests.Fixtures;

namespace ClipMate.UITests.PageObjects;

/// <summary>
/// The clip list in ClipMate's main window, driven through UI Automation.
/// <para>
/// Each row's automation name is its cells joined by "; ", starting with the format icons and the title, e.g.
/// "📁📄; C:\Users\me\AppData\Local\Temp\...; 600; 10/22/2026 7:10:02 AM -05:00; ...". Titles are truncated, so
/// match long content by its icons (<see cref="FilesIcon" />) rather than its text.
/// </para>
/// <para>
/// Selecting a row puts that clip on the clipboard ("Pick, Flip and Paste"). After capturing a clip, ClipMate also
/// copies the new clip back by itself once its selection settles, which can overwrite clipboard content a test set
/// in the meantime.
/// </para>
/// </summary>
public class ClipListPage(Process app)
{
    /// <summary>Icon shown on clips that carry a file list.</summary>
    public const string FilesIcon = "📁";

    /// <summary>Icon shown on clips that carry text.</summary>
    public const string TextIcon = "📄";

    /// <summary>Icon shown on clips that carry an image.</summary>
    public const string ImageIcon = "🖼";

    private const string _mainWindowTitle = "ClipMate - Clipboard Manager";

    /// <summary>
    /// The first row whose automation name contains <paramref name="text" />, or null if none does (yet).
    /// The grid recreates its rows when a clip is added, so do not keep the result across captures.
    /// </summary>
    public AutomationElement? FindRow(string text)
    {
        return MainWindow()?.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem))
            .Cast<AutomationElement>()
            .FirstOrDefault(p => p.Current.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Waits until a row containing <paramref name="text" /> is listed.
    /// </summary>
    public Task<AutomationElement> WaitForRowAsync(string text) =>
        Poll.UntilAsync(() => FindRow(text), $"a clip containing \"{text}\" in the clip list");

    /// <summary>
    /// Adds the row containing <paramref name="text" /> to the selection, as Ctrl+click does.
    /// </summary>
    public async Task AddToSelectionAsync(string text)
    {
        var row = await WaitForRowAsync(text);
        ((SelectionItemPattern)row.GetCurrentPattern(SelectionItemPattern.Pattern)).AddToSelection();
    }

    /// <summary>
    /// True when the row containing <paramref name="text" /> is listed and selected.
    /// </summary>
    public bool IsSelected(string text) =>
        FindRow(text) is { } row && ((SelectionItemPattern)row.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected;

    /// <summary>
    /// Clicks the main window's toolbar button named <paramref name="name" /> (e.g. "PowerPaste"), or its check button
    /// (e.g. "Append").
    /// </summary>
    public void ClickToolbarButton(string name)
    {
        var button = MainWindow()?.FindFirst(TreeScope.Descendants, new AndCondition(
                         new OrCondition(
                             new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                             new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox)),
                         new PropertyCondition(AutomationElement.NameProperty, name)))
                     ?? throw new InvalidOperationException($"No toolbar button named \"{name}\"");

        if (button.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
            ((InvokePattern)invoke).Invoke();
        else
            ((TogglePattern)button.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
    }

    /// <summary>
    /// Right-clicks the row containing <paramref name="rowText" /> with the mouse, as a user opens the clip list's context
    /// menu, and clicks the menu's item named <paramref name="name" />.
    /// </summary>
    public async Task ClickContextMenuItemAsync(string rowText, string name)
    {
        var window = MainWindow() ?? throw new InvalidOperationException("The main window is not open");
        await Foreground.ActivateAsync(window.Current.NativeWindowHandle, "ClipMate's main window");
        var row = await WaitForRowAsync(rowText);
        Foreground.RightClick(row.Current.BoundingRectangle);

        var item = await Poll.UntilAsync(() => AutomationElement.RootElement.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, app.Id),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                new PropertyCondition(AutomationElement.NameProperty, name))),
            $"the context menu item \"{name}\"");

        ((InvokePattern)item.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private AutomationElement? MainWindow() =>
        AutomationElement.RootElement.FindFirst(TreeScope.Children, new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, app.Id),
            new PropertyCondition(AutomationElement.NameProperty, _mainWindowTitle)));

    /// <summary>
    /// Selects the row containing <paramref name="text" />, which copies that clip to the clipboard.
    /// The row is looked up afresh, so a reference made stale by a newer clip is never used.
    /// </summary>
    public async Task SelectAsync(string text)
    {
        var row = await WaitForRowAsync(text);
        ((SelectionItemPattern)row.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    }
}
