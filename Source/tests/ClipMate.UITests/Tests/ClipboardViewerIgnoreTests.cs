using System.IO;
using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// A copy that carries "Clipboard Viewer Ignore" (as ClipMate 7, password managers and Power Paste's re-arming set it)
/// is not captured while "pay attention to the clipboard ignore flag" is on, the default.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class ClipboardViewerIgnoreTests(AppFixture appFixture)
{
    private const string _ignoreMarker = "Clipboard Viewer Ignore";
    private const string _ignoredLogLine = "Not capturing a clipboard change marked to be ignored";

    [Test]
    public async Task CopyMarkedToBeIgnored_IsNotCaptured()
    {
        var ignored = $"ignored {Guid.NewGuid():N}";
        var probe = $"probe {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);

            // Act: a copy marked to be ignored
            SystemClipboard.OnSta(() =>
            {
                var copy = new DataObject();
                copy.SetText(ignored);
                copy.SetData(_ignoreMarker, new MemoryStream([0, 0, 0, 0]));
                Clipboard.SetDataObject(copy, true);
            });

            // Assert: ClipMate looked at the change and left it out; an ordinary copy made afterwards is still captured
            await Poll.UntilAsync(() => appFixture.ReadLog().Contains(_ignoredLogLine, StringComparison.Ordinal),
                "ClipMate to pass over the marked copy");

            var attempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (attempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetText(probe));

                return clipList.FindRow(probe);
            }, "the ordinary copy in the clip list");

            await Assert.That(clipList.FindRow(ignored)).IsNull();
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }
}
