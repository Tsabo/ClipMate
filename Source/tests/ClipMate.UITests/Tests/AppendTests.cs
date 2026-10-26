using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Append (Glue): selecting several text clips and clicking Append makes one new clip of their text.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class AppendTests(AppFixture appFixture)
{
    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    private readonly string[] _links = Enumerable.Range(1, 3).Select(p => $"https://example.com/{p}?{Guid.NewGuid():N}").ToArray();

    [Test]
    public async Task ThreeTextClipsSelectedBottomUp_AreAppendedBottomUpAndPutOnTheClipboard() =>
        // The oldest link is listed at the bottom
        await AppendAsync(_links);

    [Test]
    public async Task ThreeTextClipsSelectedTopDown_AreAppendedTopDown() =>
        await AppendAsync(_links.Reverse().ToArray());

    [Test]
    public async Task AnHtmlClipWithPlainText_IsAppendedByItsPlainText() =>
        // Copied from a browser's address bar: HTML plus the link as plain text
        await AppendAsync(_links, htmlLink: _links[1]);

    [Test]
    public async Task AnImageClipAmongTheSelection_IsSkipped()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: two links and an image between them, all selected
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, _links[0], false);
            await CopyImageUntilListedAsync(clipList);
            await CopyUntilListedAsync(clipList, _links[1], false);

            await clipList.SelectAsync(_links[0]);
            await clipList.AddToSelectionAsync(ClipListPage.ImageIcon);
            await clipList.AddToSelectionAsync(_links[1]);
            await Poll.UntilAsync(() => clipList.IsSelected(ClipListPage.ImageIcon) && clipList.IsSelected(_links[1]), "all three clips to be selected");

            // Act
            clipList.ClickToolbarButton("Append");

            // Assert: the two links are appended, without the image
            await clipList.WaitForRowAsync("Appended");
            var glued = _links[0] + "\r\n" + _links[1];
            await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process) && SystemClipboard.TryReadText(IntPtr.Zero) == glued,
                "the two links appended on the clipboard");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task OnlyOneSelectedClipWithText_AppendsNothing()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: a link and an image, both selected
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, _links[0], false);
            await CopyImageUntilListedAsync(clipList);

            await clipList.SelectAsync(_links[0]);
            await clipList.AddToSelectionAsync(ClipListPage.ImageIcon);
            await Poll.UntilAsync(() => clipList.IsSelected(ClipListPage.ImageIcon) && clipList.IsSelected(_links[0]), "both clips to be selected");

            // Act
            clipList.ClickToolbarButton("Append");

            // Assert: no clip of one link is made
            await Poll.UntilAsync(() => clipList.FindRow("Appended") != null || appFixture.ReadLog().Contains("Nothing to append"),
                "Append to finish");
            await Assert.That(clipList.FindRow("Appended")).IsNull();
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    /// <summary>
    /// Copies the links, selects them in <paramref name="selectionOrder" /> and clicks Append. A new clip is listed, and
    /// is on the clipboard with the links in that order, one per line, ready to paste. The links are copied as text only,
    /// as Edge's Copy Link does, except <paramref name="htmlLink" />, which is copied as HTML plus its plain text.
    /// </summary>
    private async Task AppendAsync(string[] selectionOrder, string? htmlLink = null)
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: the three links copied and all selected
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            foreach (var item in _links)
                await CopyUntilListedAsync(clipList, item, item == htmlLink);

            await clipList.SelectAsync(selectionOrder[0]);
            foreach (var item in selectionOrder.Skip(1))
                await clipList.AddToSelectionAsync(item);

            await Poll.UntilAsync(() => _links.All(clipList.IsSelected), "all three clips to be selected");

            // Act
            clipList.ClickToolbarButton("Append");

            // Assert
            await clipList.WaitForRowAsync("Appended");
            var glued = string.Join("\r\n", selectionOrder);
            await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process) && SystemClipboard.TryReadText(IntPtr.Zero) == glued,
                "the appended clip on the clipboard");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    // A copy made while the previous one is still being captured can be missed; copy again until it is listed
    private static Task CopyUntilListedAsync(ClipListPage clipList, string text, bool asHtml)
    {
        var attempt = 0;
        return Poll.UntilAsync(() =>
        {
            if (attempt++ % 10 == 0)
            {
                SystemClipboard.OnSta(() =>
                {
                    if (asHtml)
                        Clipboard.SetDataObject(Html($"<a href=\"{text}\">{text}</a>", text), true);
                    else
                        Clipboard.SetText(text);
                });
            }

            return clipList.FindRow(text);
        }, $"\"{text}\" in the clip list");
    }

    private static Task CopyImageUntilListedAsync(ClipListPage clipList)
    {
        var attempt = 0;
        return Poll.UntilAsync(() =>
        {
            if (attempt++ % 10 == 0)
            {
                SystemClipboard.OnSta(() =>
                {
                    var pixels = new byte[16 * 16 * 4];
                    Random.Shared.NextBytes(pixels);
                    Clipboard.SetImage(BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4));
                });
            }

            return clipList.FindRow(ClipListPage.ImageIcon);
        }, "an image clip in the clip list");
    }

    /// <summary>
    /// A copy offering <paramref name="fragment" /> as CF_HTML, with <paramref name="text" /> as its plain text.
    /// </summary>
    private static DataObject Html(string fragment, string text)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string before = "<html><body><!--StartFragment-->";
        const string after = "<!--EndFragment--></body></html>";
        var headerLength = string.Format(header, 0, 0, 0, 0).Length;
        var startFragment = headerLength + Encoding.UTF8.GetByteCount(before);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(after);

        var data = new DataObject();
        data.SetData(DataFormats.Html, string.Format(header, headerLength, endHtml, startFragment, endFragment) + before + fragment + after);
        data.SetText(text);
        return data;
    }
}
