using System.Windows;
using System.Windows.Automation;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Items of the clip list's right-click menu, opened with the mouse as a user does.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class ClipListContextMenuTests(AppFixture appFixture)
{
    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    private readonly string _id = Guid.NewGuid().ToString("N");

    [Test]
    public async Task PlaceSourceUrlOnClipboard_PutsThePagesUrlOnTheClipboard()
    {
        var text = $"copied text {_id}";
        var page = $"https://example.com/page?{_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: text copied from a web page
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, text, () => HtmlCopy.Create($"<b>{text}</b>", text, page));

            // Act
            await clipList.ClickContextMenuItemAsync(text, "Place Source URL On Clipboard");

            // Assert: the page's URL is on the clipboard as ClipMate's own copy
            await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process) && SystemClipboard.TryReadText(IntPtr.Zero) == page,
                "the source URL on the clipboard");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task SelectAnotherCollection_ShowsTheCollectionPicked()
    {
        var text = $"in Inbox {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: a clip in Inbox, the collection shown
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, text, () =>
            {
                var data = new DataObject();
                data.SetText(text);
                return data;
            });

            // Act: pick Safe
            await clipList.ClickContextMenuItemAsync(text, "Select Another Collection");
            await PickCollectionAsync("Safe");

            // Assert: the clip list shows Safe, which doesn't hold the Inbox clip
            await Poll.UntilAsync(() => clipList.FindRow(text) == null, "the clip list to show Safe");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task ToggleMacros_MarksTheClipAsAMacroAndSavesIt()
    {
        var text = $"macro text {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: a text clip, not a macro
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, text, () =>
            {
                var data = new DataObject();
                data.SetText(text);
                return data;
            });

            // Act
            await clipList.ClickContextMenuItemAsync(text, "Toggle Macros On/Off For This Clip");

            // Assert: the row shows the macro icon, and the flag is saved
            await Poll.UntilAsync(() => clipList.FindRow(text)?.Current.Name.StartsWith(_macroIcon, StringComparison.Ordinal) == true,
                "the clip to show the macro icon");
            await Assert.That(IsSavedAsMacro(text)).IsTrue();
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    // The clip list's icon for a macro clip
    private const string _macroIcon = "⌨";

    private bool IsSavedAsMacro(string text)
    {
        using var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT c.Macro FROM Clips c JOIN BlobTxt b ON b.ClipId = c.Id WHERE b.Data = $text";
        command.Parameters.AddWithValue("$text", text);
        return Convert.ToBoolean(command.ExecuteScalar());
    }

    // Chooses a collection in the Select Collection dialog and clicks OK. UI Automation lists the dialog under the
    // window that owns it.
    private async Task PickCollectionAsync(string name)
    {
        var dialog = await Poll.UntilAsync(() => AutomationElement.RootElement.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, appFixture.Process.Id),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
            new PropertyCondition(AutomationElement.NameProperty, "Select Collection"))), "the Select Collection dialog");

        // Tree items are named "Level <depth>: <name>"; the default database opens expanded
        var collection = await Poll.UntilAsync(() => dialog
                .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem))
                .Cast<AutomationElement>()
                .FirstOrDefault(p => p.Current.Name.EndsWith($": {name}", StringComparison.Ordinal)),
            $"\"{name}\" in the Select Collection dialog");
        Foreground.LeftClick(collection.Current.BoundingRectangle);

        var ok = await Poll.UntilAsync(() => dialog.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.NameProperty, "OK"))) is { Current.IsEnabled: true } button ? button : null, "the OK button to be enabled");

        // The dialog's buttons don't offer Invoke to UI Automation
        Foreground.LeftClick(ok.Current.BoundingRectangle);
    }

    // A copy made while ClipMate is still settling can be missed; copy again until it is listed
    private static Task CopyUntilListedAsync(ClipListPage clipList, string text, Func<DataObject> copy)
    {
        var attempt = 0;
        return Poll.UntilAsync(() =>
        {
            if (attempt++ % 10 == 0)
                SystemClipboard.OnSta(() => Clipboard.SetDataObject(copy(), true));

            return clipList.FindRow(text);
        }, $"\"{text}\" in the clip list");
    }
}
