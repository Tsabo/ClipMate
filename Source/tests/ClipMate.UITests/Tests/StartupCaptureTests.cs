using System.IO;
using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// "Capture existing clipboard at startup" (on by default) takes what is on the clipboard when ClipMate starts, even
/// when it carries "Clipboard Viewer Ignore": like Capture Special, it skips the ignore flag.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class StartupCaptureTests(AppFixture appFixture)
{
    [Test]
    public async Task ClipboardMarkedToBeIgnored_IsCapturedAtStartup()
    {
        var existing = $"existing {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            SystemClipboard.OnSta(() =>
            {
                var copy = new DataObject();
                copy.SetText(existing);
                copy.SetData("Clipboard Viewer Ignore", new MemoryStream([0, 0, 0, 0]));
                Clipboard.SetDataObject(copy, true);
            });

            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);

            await Poll.UntilAsync(() => clipList.FindRow(existing), "the clipboard's content at startup in the clip list");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }
}
