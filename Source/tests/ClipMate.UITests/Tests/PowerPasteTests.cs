using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Power Paste offers the waiting clip by delayed rendering: a read by the application in the foreground is a paste
/// and advances the sequence, and a read by a program in the background (clipboard history, Remote Desktop, another
/// clipboard manager) gets nothing and changes nothing (ADR-0008). When Power Paste ends, the waiting clip stays on the
/// clipboard as a normal copy; when another program takes the clipboard, its copy is left alone.
/// <para>
/// The test process pastes through a window of its own brought to the foreground, and a separate helper process reads
/// from a hidden window as the background program. A read without a window counts as a paste, so while Power Paste
/// runs the test waits on the clipboard's owner and sequence number instead of reading it. Uses the real, shared
/// clipboard and puts its previous content back afterwards.
/// </para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class PowerPasteTests(AppFixture appFixture)
{
    // The title of the window through which Power Paste owns the clipboard
    private const string _powerPasteWindow = "ClipMate.PowerPaste";

    // ClipMate marks its own copies with this; Power Paste's offers carry it among the markers that keep readers away
    private const string _ignoreMarker = "Clipboard Viewer Ignore";

    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    private readonly string _older = $"power paste older {Guid.NewGuid():N}";
    private readonly string _newer = $"power paste newer {Guid.NewGuid():N}";

    [Test]
    public async Task PasteDetection_APasteByTheForegroundApplicationAdvances_ABackgroundReadGetsNothing()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Arrange: Power Paste Down over both clips (a saved direction); the newer one is listed first, so it waits first
            var clipList = await StartPowerPasteOverBothClipsAsync();
            var formats = await Poll.UntilAsync(SystemClipboard.TryGetRawFormatNames, "the offered formats");
            using var target = await ForegroundTestWindow.OpenAsync();
            var offered = SystemClipboard.SequenceNumber;

            // Act: a program in the background reads while the target is in front
            bool gotNothing;
            using (var reader = ClipboardTestProgram.ReadInBackground())
            {
                await Poll.UntilAsync(() => reader.HasPrinted("read:(null)") || reader.HasPrinted($"read:{_newer}"), "the background read");
                gotNothing = reader.HasPrinted("read:(null)");
            }

            // Assert: the offer carries every marker, the background program got nothing, and the clipboard didn't change
            await Assert.That(gotNothing).IsTrue();
            await Assert.That(formats).Contains(_ignoreMarker);
            await Assert.That(formats).Contains("ExcludeClipboardContentFromMonitorProcessing");
            await Assert.That(formats).Contains("CanIncludeInClipboardHistory");
            await Assert.That(formats).Contains("CanUploadToCloudClipboard");
            await Assert.That(SystemClipboard.SequenceNumber).IsEqualTo(offered);
            await Assert.That(SystemClipboard.GetOwnerWindowTitle()).IsEqualTo(_powerPasteWindow);

            // Act: a paste into the foreground application
            var pasted = await target.PasteAsync();

            // Assert: it got the waiting clip, and the next clip went up, which the clip list now shows as current while
            // both clips of the sequence stay selected
            await Assert.That(pasted).IsEqualTo(_newer);
            await Poll.UntilAsync(() => SystemClipboard.SequenceNumber != offered && SystemClipboard.GetOwnerWindowTitle() == _powerPasteWindow,
                "Power Paste to offer the next clip");
            await Poll.UntilAsync(() => clipList.IsSelected(_newer) && clipList.IsSelected(_older), "both clips to stay selected");

            // Act: paste the last clip
            await Assert.That(await target.PasteAsync()).IsEqualTo(_older);

            // Assert: Power Paste finishes, and the last clip stays on the clipboard as a normal copy
            await Poll.UntilAsync(() => SystemClipboard.GetOwnerWindowTitle() != _powerPasteWindow, "Power Paste to finish after the last clip is pasted");
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
            // Arrange: Power Paste Down, where the newer clip waits first
            var clipList = await StartPowerPasteOverBothClipsAsync();

            // Act: the next click stops Power Paste
            clipList.ClickToolbarButton("PowerPaste");

            // Assert
            await AssertLeftOnTheClipboardAsNormalCopyAsync(_newer);
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
        await appFixture.StartAppAsync("preferences.power_paste_last_direction=\"Down\"");
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

        // The newer clip, just captured, is already the current row, so selecting it copies nothing. Adding the older
        // one makes it current, and ClipMate copies it once the selection settles: that copy must land before Power
        // Paste takes the clipboard, or it would stop Power Paste as a copy made elsewhere does.
        await clipList.SelectAsync(_newer);
        await clipList.AddToSelectionAsync(_older);
        await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process) && SystemClipboard.TryReadText(IntPtr.Zero) == _older,
            "ClipMate to copy the older clip it was given as current");

        clipList.ClickToolbarButton("PowerPaste");
        await Poll.UntilAsync(() => SystemClipboard.GetOwnerWindowTitle() == _powerPasteWindow, "Power Paste to arm the first clip");

        return clipList;
    }

    private async Task AssertLeftOnTheClipboardAsNormalCopyAsync(string text)
    {
        await Poll.UntilAsync(() => SystemClipboard.GetOwnerWindowTitle() != _powerPasteWindow && SystemClipboard.IsOwnedBy(appFixture.Process) &&
                                    SystemClipboard.TryReadText(IntPtr.Zero) == text,
            "ClipMate to leave the waiting clip on the clipboard");

        // Marked as ClipMate's own copy like any other, but kept in Windows clipboard history, unlike an offer
        var formats = await Poll.UntilAsync(SystemClipboard.TryGetRawFormatNames, "the clipboard's formats");
        await Assert.That(formats).Contains("#13"); // CF_UNICODETEXT
        await Assert.That(formats).Contains(_ignoreMarker);
        await Assert.That(formats).DoesNotContain("ExcludeClipboardContentFromMonitorProcessing");
    }
}
