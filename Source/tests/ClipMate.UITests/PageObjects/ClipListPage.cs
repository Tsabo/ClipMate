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
        var window = AutomationElement.RootElement.FindFirst(TreeScope.Children, new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, app.Id),
            new PropertyCondition(AutomationElement.NameProperty, _mainWindowTitle)));

        return window?.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem))
            .Cast<AutomationElement>()
            .FirstOrDefault(p => p.Current.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Waits until a row containing <paramref name="text" /> is listed.
    /// </summary>
    public Task<AutomationElement> WaitForRowAsync(string text) =>
        Poll.UntilAsync(() => FindRow(text), $"a clip containing \"{text}\" in the clip list");

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
