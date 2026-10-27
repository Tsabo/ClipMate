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
            await new WindowsPage(appFixture.Process).PickCollectionAsync("Safe");

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

    [Test]
    public async Task UnDelete_RestoresAClipFromTheTrashToTheCollectionPicked()
    {
        var text = $"deleted text {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: a clip sent to the Trash, selected there
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            var windows = new WindowsPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, text, () =>
            {
                var data = new DataObject();
                data.SetText(text);
                return data;
            });

            await clipList.ClickContextMenuItemAsync(text, "Delete Selected Items");
            await windows.ClickDialogButtonAsync("Confirm Delete", "Yes");
            await Poll.UntilAsync(() => IsInTrash(text), "the clip to be in the Trash");

            await windows.SelectTreeNodeAsync("Trashcan");
            await clipList.SelectAsync(text);

            // Act: Un Delete, into Safe
            await clipList.ClickContextMenuItemAsync(text, "Un Delete");
            await windows.PickCollectionAsync("Safe", "Restore to Collection");

            // Assert: out of the Trash, in Safe, and gone from the Trash view
            await Poll.UntilAsync(() => !IsInTrash(text), "the clip to be restored");
            await Assert.That(CollectionOf(text)).IsEqualTo("Safe");
            await Poll.UntilAsync(() => clipList.FindRow(text) == null, "the clip to leave the Trash view");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    private string? CollectionOf(string text)
    {
        using var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT co.Name FROM Clips c JOIN BlobTxt b ON b.ClipId = c.Id JOIN Collections co ON co.Id = c.CollectionId WHERE b.Data = $text";
        command.Parameters.AddWithValue("$text", text);
        return command.ExecuteScalar() as string;
    }

    private bool IsInTrash(string text)
    {
        using var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT c.Del FROM Clips c JOIN BlobTxt b ON b.ClipId = c.Id WHERE b.Data = $text";
        command.Parameters.AddWithValue("$text", text);
        return Convert.ToBoolean(command.ExecuteScalar());
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
