using System.IO;
using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Capturing a copy must leave the source application's data on the clipboard. If ClipMate replaced it with its own
/// copy of the clip, every Format it does not store (another app's private formats) would be stripped a moment after
/// copying, and pasting would give ClipMate's version instead of the original.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class CaptureLeavesClipboardAloneTests(AppFixture appFixture)
{
    private const string _privateFormat = "ClipMate.UITests.PrivateFormat";

    [Test]
    public async Task CopiedText_AfterCapture_TheSourcesOwnDataStaysOnTheClipboard()
    {
        var text = $"copied {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);

            // Arrange: text plus a private format ClipMate does not store, as many applications offer
            SystemClipboard.OnSta(() =>
            {
                var copy = new DataObject();
                copy.SetText(text);
                copy.SetData(_privateFormat, new MemoryStream([1, 2, 3]));
                Clipboard.SetDataObject(copy, true);
            });

            // Act: ClipMate captures and lists the copy
            await clipList.WaitForRowAsync(text);

            // Assert: well past the 400 ms selection debounce that used to copy it back, the clipboard still holds the
            // source's own data, private format included
            var tookOver = false;
            try
            {
                await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process), "ClipMate to take over the clipboard",
                    TimeSpan.FromSeconds(3));
                tookOver = true;
            }
            catch (TimeoutException)
            {
                // Expected: ClipMate never replaced the clipboard
            }

            await Assert.That(tookOver).IsFalse();
            await Assert.That(SystemClipboard.TryGetRawFormatNames()).Contains(_privateFormat);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }
}
