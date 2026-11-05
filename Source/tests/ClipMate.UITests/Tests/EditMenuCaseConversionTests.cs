using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Edit > Case Conversion in the main window, opened with UI Automation as a user opens the menu.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class EditMenuCaseConversionTests(AppFixture appFixture)
{
    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    private readonly string _id = Guid.NewGuid().ToString("N");

    [Test]
    public async Task Uppercase_ConvertsTheSelectedClipAndSavesIt()
    {
        var text = $"Mixed text {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: a text clip, selected
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, text);
            await clipList.SelectAsync(text);

            // Act
            await clipList.ClickMenuItemAsync("Edit", "Case Conversion", "UPPER CASE");

            // Assert: the saved text and the clip's title are converted, and the new text is on the clipboard
            var converted = text.ToUpperInvariant();
            await Poll.UntilAsync(() => SavedTextIs(converted), "the clip's saved text to be converted");
            await clipList.WaitForRowAsync(converted);
            await Assert.That(clipList.FindRow(converted)!.Current.Name).Contains(converted);
            await Poll.UntilAsync(() => SystemClipboard.TryReadText(IntPtr.Zero) == converted, "the new text on the clipboard");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task Uppercase_ConvertsAClipJustCaptured()
    {
        var text = $"Just captured {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: a clip ClipMate selected by itself when capturing it
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, text);
            await Poll.UntilAsync(() => clipList.IsSelected(text), "the captured clip to be selected");

            // Act
            await clipList.ClickMenuItemAsync("Edit", "Case Conversion", "UPPER CASE");

            // Assert
            await Poll.UntilAsync(() => SavedTextIs(text.ToUpperInvariant()), "the clip's saved text to be converted");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task Uppercase_ConvertsTheClipJustCaptured_NotTheOneSelectedBefore()
    {
        var earlier = $"Earlier clip {_id}";
        var captured = $"Captured clip {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: a clip the user selected, then a newer capture
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, earlier);
            await clipList.SelectAsync(earlier);
            await CopyUntilListedAsync(clipList, captured);
            await Poll.UntilAsync(() => clipList.IsSelected(captured), "the captured clip to be selected");

            // Act
            await clipList.ClickMenuItemAsync("Edit", "Case Conversion", "UPPER CASE");

            // Assert: the capture is converted, and the earlier clip is left alone
            await Poll.UntilAsync(() => SavedTextIs(captured.ToUpperInvariant()), "the captured clip to be converted");
            await Assert.That(SavedTextIs(earlier)).IsTrue();
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task Shortcut_ConvertsTheSelectedClip()
    {
        var text = $"Mixed Text {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: a text clip, selected
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, text);
            await clipList.SelectAsync(text);

            // Act: Ctrl+Alt+L
            await clipList.PressKeysInMainWindowAsync(Foreground.VkControl, Foreground.VkAlt, _vkL);

            // Assert
            await Poll.UntilAsync(() => SavedTextIs(text.ToLowerInvariant()), "the clip's saved text to be converted");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task Uppercase_ConvertsEverySelectedClip()
    {
        var first = $"First clip {_id}";
        var second = $"Second clip {_id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: two text clips, both selected
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, first);
            await CopyUntilListedAsync(clipList, second);
            await clipList.SelectAsync(first);
            await clipList.AddToSelectionAsync(second);
            await Poll.UntilAsync(() => clipList.IsSelected(first) && clipList.IsSelected(second), "both clips to be selected");

            // Act
            await clipList.ClickMenuItemAsync("Edit", "Case Conversion", "UPPER CASE");

            // Assert
            await Poll.UntilAsync(() => SavedTextIs(first.ToUpperInvariant()) && SavedTextIs(second.ToUpperInvariant()), "both clips to be converted");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    private const ushort _vkL = 0x4C;

    private bool SavedTextIs(string text)
    {
        using var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM BlobTxt WHERE Data = $text";
        command.Parameters.AddWithValue("$text", text);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    // A copy made while ClipMate is still settling can be missed; copy again until it is listed
    private static Task CopyUntilListedAsync(ClipListPage clipList, string text)
    {
        var attempt = 0;
        return Poll.UntilAsync(() =>
        {
            if (attempt++ % 10 == 0)
                SystemClipboard.OnSta(() => Clipboard.SetDataObject(text, true));

            return clipList.FindRow(text);
        }, $"\"{text}\" in the clip list");
    }
}
