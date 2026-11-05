using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Edit > Remove Linebreaks, Strip Out Non-TEXT Data Formats and Clean-Up Text in the main window, opened with the mouse
/// as a user opens the menu.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class EditMenuTextCommandsTests(AppFixture appFixture)
{
    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    private readonly string _id = Guid.NewGuid().ToString("N");

    [Test]
    public async Task RemoveLinebreaks_JoinsTheLinesOfTheSelectedClip()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, new DataObject(DataFormats.UnicodeText, $"{_id} one\r\ntwo"), _id);
            await clipList.SelectAsync(_id);

            // Act
            await clipList.ClickMenuItemAsync("Edit", "Remove Linebreaks");

            // Assert: saved, and on the clipboard
            var joined = $"{_id} one two";
            await Poll.UntilAsync(() => SavedTextIs(joined), "the clip's saved text to be joined");
            await Poll.UntilAsync(() => SystemClipboard.TryReadText(IntPtr.Zero) == joined, "the new text on the clipboard");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task StripNonText_RemovesTheOtherFormatsOfEverySelectedClip()
    {
        var first = $"First {_id}";
        var second = $"Second {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: two clips with HTML, both selected
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, HtmlAndText(first), first);
            await CopyUntilListedAsync(clipList, HtmlAndText(second), second);
            await Poll.UntilAsync(() => FormatCount(first) > 1 && FormatCount(second) > 1, "the clips to be saved with HTML");
            await clipList.SelectAsync(first);
            await clipList.AddToSelectionAsync(second);
            await Poll.UntilAsync(() => clipList.IsSelected(first) && clipList.IsSelected(second), "both clips to be selected");

            // Act
            await clipList.ClickMenuItemAsync("Edit", "Strip Out Non-TEXT Data Formats");

            // Assert: only the text is left
            await Poll.UntilAsync(() => FormatCount(first) == 1 && FormatCount(second) == 1, "both clips to keep only their text");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task CleanUpText_OK_StripsLeadingQuoteMarksFromTheSelectedClip()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, new DataObject(DataFormats.UnicodeText, $"> {_id} one\r\n>  two"), _id);
            await clipList.SelectAsync(_id);

            // Act: the dialog's first-use choices strip >, spaces and tabs from the start of each line
            await clipList.ClickMenuItemAsync("Edit", "Clean-Up Text...");
            await new WindowsPage(appFixture.Process).ClickDialogButtonAsync("Text Cleanup Options", "OK");

            // Assert
            await Poll.UntilAsync(() => SavedTextIs($"{_id} one\r\ntwo"), "the clip's saved text to be cleaned up");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task CleanUpText_Cancel_LeavesTheClipAlone()
    {
        var text = $"> {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, new DataObject(DataFormats.UnicodeText, text), _id);
            await clipList.SelectAsync(_id);

            // Act
            await clipList.ClickMenuItemAsync("Edit", "Clean-Up Text...");
            var windows = new WindowsPage(appFixture.Process);
            await windows.ClickDialogButtonAsync("Text Cleanup Options", "Cancel");

            // Assert: the menu opens again, so the dialog closed, and nothing changed
            await clipList.ClickMenuItemAsync("Edit", "Clean-Up Text...");
            await windows.ClickDialogButtonAsync("Text Cleanup Options", "Cancel");
            await Assert.That(SavedTextIs(text)).IsTrue();
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    private static DataObject HtmlAndText(string text)
    {
        const string header = "Version:0.9\r\nStartHTML:0000000097\r\nEndHTML:{0:D10}\r\nStartFragment:0000000097\r\nEndFragment:{0:D10}\r\n";
        var html = $"<b>{text}</b>";
        var data = new DataObject(DataFormats.UnicodeText, text);
        data.SetData(DataFormats.Html, string.Format(header, 97 + html.Length) + html);
        return data;
    }

    private bool SavedTextIs(string text)
    {
        using var connection = OpenDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM BlobTxt WHERE Data = $text";
        command.Parameters.AddWithValue("$text", text);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private int FormatCount(string text)
    {
        using var connection = OpenDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ClipData WHERE ClipId = (SELECT ClipId FROM BlobTxt WHERE Data = $text LIMIT 1)";
        command.Parameters.AddWithValue("$text", text);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        return connection;
    }

    // A copy made while ClipMate is still settling can be missed; copy again until it is listed
    private static Task CopyUntilListedAsync(ClipListPage clipList, DataObject data, string rowText)
    {
        var attempt = 0;
        return Poll.UntilAsync(() =>
        {
            if (attempt++ % 10 == 0)
                SystemClipboard.OnSta(() => Clipboard.SetDataObject(data, true));

            return clipList.FindRow(rowText);
        }, $"\"{rowText}\" in the clip list");
    }
}
