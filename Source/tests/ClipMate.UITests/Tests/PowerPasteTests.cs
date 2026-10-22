using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Power Paste offers the waiting clip by delayed rendering: a read by the application in the foreground is a paste
/// and advances the sequence, any other read (clipboard history, another clipboard manager) gets the data without
/// advancing. When Power Paste ends, the waiting clip stays on the clipboard as a normal copy; when another program
/// takes the clipboard, its copy is left alone.
/// <para>
/// The test process plays both readers: it reads without a window as a background reader, and through a window of
/// its own brought to the foreground as the application the user pastes into. Uses the real, shared clipboard and
/// puts its previous content back afterwards.
/// </para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class PowerPasteTests(AppFixture appFixture)
{
    // The title of the window through which Power Paste owns the clipboard
    private const string _powerPasteWindow = "ClipMate.PowerPaste";

    // The marker Power Paste adds when it re-arms the waiting clip after a read that was not a paste
    private const string _ignoreMarker = "Clipboard Viewer Ignore";

    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    private readonly string _older = $"power paste older {Guid.NewGuid():N}";
    private readonly string _newer = $"power paste newer {Guid.NewGuid():N}";

    [Test]
    public async Task PasteDetection_APasteByTheForegroundApplicationAdvances_ABackgroundReadDoesNot()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: Power Paste Down over both clips; the newer one is listed first, so it waits first
            await StartPowerPasteOverBothClipsAsync();

            // Act: a background read, as clipboard history or another clipboard manager makes
            var backgroundRead = SystemClipboard.TryReadText(IntPtr.Zero);

            // Assert: it got the waiting clip, and Power Paste armed that same clip again instead of advancing
            await Assert.That(backgroundRead).IsEqualTo(_newer);
            await Poll.UntilAsync(() => SystemClipboard.TryGetRawFormatNames()?.Contains(_ignoreMarker) == true,
                "Power Paste to re-arm the waiting clip");
            await Assert.That(SystemClipboard.TryReadText(IntPtr.Zero)).IsEqualTo(_newer);

            // Act: a paste into the foreground application
            using var target = await ForegroundTestWindow.OpenAsync();
            var pasted = await target.PasteAsync();

            // Assert: it got the waiting clip, and the sequence moved on to the next one
            await Assert.That(pasted).IsEqualTo(_newer);
            await Poll.UntilAsync(() => SystemClipboard.TryReadText(IntPtr.Zero) == _older, "Power Paste to advance to the next clip");

            // Act: paste the last clip. A paste within 500 ms of the previous one counts as the application re-reading
            // it, so keep pasting until the sequence finishes.
            // A later attempt can land while ClipMate is putting the clip back, so only the first one's text is checked.
            await Assert.That(await target.PasteAsync()).IsEqualTo(_older);
            var deadline = DateTime.UtcNow + Poll.DefaultTimeout;
            while (SystemClipboard.GetOwnerWindowTitle() == _powerPasteWindow && DateTime.UtcNow < deadline)
            {
                await Task.Delay(200);
                await target.PasteAsync();
            }

            // Assert: the last clip stays on the clipboard as a normal copy
            await AssertLeftOnTheClipboardAsNormalCopyAsync(_older);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task Stopping_LeavesTheWaitingClipOnTheClipboard()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: Power Paste Down, then the toolbar button changes it to Up, where the older clip waits first
            var clipList = await StartPowerPasteOverBothClipsAsync();
            clipList.ClickToolbarButton("PowerPaste");
            await Poll.UntilAsync(() => SystemClipboard.TryReadText(IntPtr.Zero) == _older, "Power Paste Up to arm the older clip");

            // Act: the third click stops Power Paste
            clipList.ClickToolbarButton("PowerPaste");

            // Assert
            await AssertLeftOnTheClipboardAsNormalCopyAsync(_older);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task AnotherCopy_StopsPowerPasteAndStaysOnTheClipboard()
    {
        var copied = $"copied elsewhere {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange
            var clipList = await StartPowerPasteOverBothClipsAsync();

            // Act: the user copies something in another application
            SystemClipboard.OnSta(() => Clipboard.SetText(copied));

            // Assert: Power Paste let go of the clipboard and left the new copy there
            await clipList.WaitForRowAsync(copied);
            await Assert.That(SystemClipboard.GetOwnerWindowTitle()).IsNotEqualTo(_powerPasteWindow);
            await Assert.That(SystemClipboard.TryReadText(IntPtr.Zero)).IsEqualTo(copied);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    /// <summary>
    /// Copies two clips, selects both and clicks the PowerPaste toolbar button (Power Paste Down), then waits until
    /// the first clip is armed for delayed rendering.
    /// </summary>
    private async Task<ClipListPage> StartPowerPasteOverBothClipsAsync()
    {
        await appFixture.StartAppAsync();
        var clipList = new ClipListPage(appFixture.Process);

        // A copy made while the previous one is still being captured can be missed; copy again until it is listed
        foreach (var item in new[] { _older, _newer })
        {
            var attempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (attempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetText(item));

                return clipList.FindRow(item);
            }, $"\"{item}\" in the clip list");
        }

        await clipList.SelectAsync(_newer);
        await clipList.AddToSelectionAsync(_older);

        // Picking a clip copies it once the selection settles; that copy must land before Power Paste takes the
        // clipboard, or it would stop Power Paste as a copy made elsewhere does
        await WaitUntilClipboardSettlesAsync();

        clipList.ClickToolbarButton("PowerPaste");
        await Poll.UntilAsync(() => SystemClipboard.GetOwnerWindowTitle() == _powerPasteWindow, "Power Paste to arm the first clip");

        return clipList;
    }

    private static async Task WaitUntilClipboardSettlesAsync()
    {
        var quietFor = TimeSpan.FromSeconds(1.5);
        var sequenceNumber = SystemClipboard.SequenceNumber;
        var changedAt = DateTime.UtcNow;
        await Poll.UntilAsync(() =>
        {
            if (SystemClipboard.SequenceNumber != sequenceNumber)
            {
                sequenceNumber = SystemClipboard.SequenceNumber;
                changedAt = DateTime.UtcNow;
            }

            return DateTime.UtcNow - changedAt >= quietFor;
        }, "the clipboard to stop changing");
    }

    private async Task AssertLeftOnTheClipboardAsNormalCopyAsync(string text)
    {
        await Poll.UntilAsync(() => SystemClipboard.GetOwnerWindowTitle() != _powerPasteWindow && SystemClipboard.IsOwnedBy(appFixture.Process) &&
                                    SystemClipboard.TryReadText(IntPtr.Zero) == text,
            "ClipMate to leave the waiting clip on the clipboard");

        var formats = await Poll.UntilAsync(SystemClipboard.TryGetRawFormatNames, "the clipboard's formats");
        await Assert.That(formats).Contains("#13"); // CF_UNICODETEXT
        await Assert.That(formats).DoesNotContain(_ignoreMarker);
    }
}
