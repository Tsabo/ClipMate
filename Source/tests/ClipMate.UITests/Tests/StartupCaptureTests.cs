using System.IO;
using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// "Capture existing clipboard at startup" (on by default) takes what is on the clipboard when ClipMate starts, as
/// ClipMate 7 does: only text or an image, and never a clipboard carrying "Clipboard Viewer Ignore".
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class StartupCaptureTests(AppFixture appFixture)
{
    [Test]
    public async Task TextOnTheClipboard_IsCapturedAtStartup()
    {
        var existing = $"existing {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            SystemClipboard.OnSta(() => Clipboard.SetText(existing));

            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);

            await Poll.UntilAsync(() => clipList.FindRow(existing), "the clipboard's content at startup in the clip list");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task ClipboardMarkedToBeIgnored_IsNotCapturedAtStartup()
    {
        var existing = $"existing {Guid.NewGuid():N}";
        var next = $"next {Guid.NewGuid():N}";
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

            // The startup capture runs before monitoring starts, so once a later copy is listed it has been decided
            var attempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (attempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetText(next));

                return clipList.FindRow(next);
            }, "a copy made after startup in the clip list");

            await Assert.That(clipList.FindRow(existing)).IsNull();
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }
}
