using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// With the Outbound Filter on in Active mode, a copy made in another program is put back on the clipboard as plain
/// text right after it is captured (manual: Pasting Options, "Filter Outbound Clips").
/// <para>
/// Uses the real, shared clipboard, whose previous content it puts back afterwards.
/// </para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class OutboundFilterTests(AppFixture appFixture)
{
    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    private readonly string _link = $"https://example.com/o?{Guid.NewGuid():N}";

    [Test]
    public async Task ActiveFilter_AnHtmlCopy_IsPutBackAsPlainText()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange
            await appFixture.StartAppAsync("preferences.outbound_filter=true", "preferences.outbound_filter_mode=\"Active\"");
            var clipList = new ClipListPage(appFixture.Process);

            // Act: a link copied as a browser's address bar does, HTML plus plain text. A copy made while ClipMate is
            // still settling can be missed; copy again until it is listed
            var attempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (attempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetDataObject(HtmlCopy.Create($"<a href=\"{_link}\">{_link}</a>", _link), true));

                return clipList.FindRow(_link);
            }, "the link in the clip list");

            // Assert: ClipMate put the link back as plain text, without the HTML, without any key being pressed
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
}
