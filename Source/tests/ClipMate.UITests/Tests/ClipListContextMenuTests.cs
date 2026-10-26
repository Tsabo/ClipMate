using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

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
