using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// The Manual Filter hotkey leaves only the clipboard's plain text (or its image), as ClipMate 7's does.
/// <para>
/// Uses a hotkey the user's own ClipMate doesn't hold, and the real, shared clipboard, whose previous content it puts
/// back afterwards.
/// </para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class ManualFilterTests(AppFixture appFixture)
{
    private const string _hotkey = "Ctrl+Alt+Shift+F9";

    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    // and short enough for the clip list's title, which is cut off
    private readonly string _link = $"https://example.com/f?{Guid.NewGuid():N}";

    [Test]
    public async Task AnHtmlCopy_IsLeftAsPlainTextOnly()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: a link copied as a browser's address bar does, HTML plus plain text
            await appFixture.StartAppAsync($"hotkeys.manual_filter=\"{_hotkey}\"");
            var clipList = new ClipListPage(appFixture.Process);

            // A copy made while ClipMate is still settling can be missed; copy again until it is listed
            var attempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (attempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetDataObject(HtmlCopy.Create($"<a href=\"{_link}\">{_link}</a>", _link), true));

                return clipList.FindRow(_link);
            }, "the link in the clip list");

            // Act
            Foreground.PressKeys(Foreground.VkControl, Foreground.VkAlt, Foreground.VkShift, Foreground.VkF9);

            // Assert: ClipMate put the link back as plain text, without the HTML
            await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process) &&
                                        SystemClipboard.TryGetRawFormatNames() is { } formats &&
                                        !formats.Contains("HTML Format") &&
                                        SystemClipboard.TryReadText(IntPtr.Zero) == _link,
                "the clipboard to hold the link as plain text only");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task AnImageCopy_KeepsOnlyItsImage()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: an image copied with an application's private format beside it
            await appFixture.StartAppAsync($"hotkeys.manual_filter=\"{_hotkey}\"");
            var clipList = new ClipListPage(appFixture.Process);
            var attempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (attempt++ % 10 == 0)
                {
                    SystemClipboard.OnSta(() =>
                    {
                        var pixels = new byte[16 * 16 * 4];
                        Random.Shared.NextBytes(pixels);
                        var data = new DataObject();
                        data.SetImage(BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4));
                        data.SetData(_privateFormat, new MemoryStream([1, 2, 3]));
                        Clipboard.SetDataObject(data, true);
                    });
                }

                return clipList.FindRow(ClipListPage.ImageIcon);
            }, "the image in the clip list");

            // Act
            Foreground.PressKeys(Foreground.VkControl, Foreground.VkAlt, Foreground.VkShift, Foreground.VkF9);

            // Assert: ClipMate put the image back (CF_DIB is #8) without the private format
            await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process) &&
                                        SystemClipboard.TryGetRawFormatNames() is { } formats &&
                                        formats.Contains("#8") &&
                                        !formats.Contains(_privateFormat),
                "the clipboard to hold the image only");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    private const string _privateFormat = "ClipMate UI Test Private";
}
