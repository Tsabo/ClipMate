using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Power Paste as the manual's Lesson 2 uses it: pick a clip, turn Power Paste on, go to another program and press
/// Ctrl+V repeatedly. The program is a real one in a process of its own, and the text that lands in it is what counts.
/// Uses the real, shared clipboard and takes the keyboard focus.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class PowerPasteIntoAnotherProgramTests(AppFixture appFixture)
{
    // A fresh ClipMate goes up first, as ClipMate 7 does. These tests start from the newest clip and go down.
    private const string _goingDown = "preferences.power_paste_last_direction=\"Down\"";

    private const ushort _vkV = 0x56;

    // Slower than the time Power Paste treats as the program reading the same paste again, like a person pasting
    private static readonly TimeSpan _pause = TimeSpan.FromMilliseconds(700);

    // A click just after a paste comes in before the sequence moves on, and is handled after it: the 400 ms that a selection
    // takes to settle spans this delay
    private const string _slowToMoveOn = "preferences.power_paste_delay=300";

    // Pasting "very rapidly", as the manual puts it, but slower than a program's re-read
    private static readonly TimeSpan _fastPause = TimeSpan.FromMilliseconds(350);

    private readonly string _first = $"pp-a-{Guid.NewGuid():N}"[..12];
    private readonly string _second = $"pp-b-{Guid.NewGuid():N}"[..12];
    private readonly string _third = $"pp-c-{Guid.NewGuid():N}"[..12];

    [Test]
    [Arguments(TestAppKind.WinForms)]
    [Arguments(TestAppKind.Wpf)]
    [Arguments(TestAppKind.NoWindowReader)]
    public async Task OneClipSelected_RunsToTheEndOfTheCollection(TestAppKind kind)
    {
        var backup = SystemClipboard.Backup();
        try
        {
            var clipList = await CopyThreeClipsAsync(_goingDown);
            await SelectAndWaitForCopyAsync(clipList, _third);

            clipList.ClickToolbarButton("PowerPaste");
            await WaitForPowerPasteToArmAsync();

            using var target = await TestAppWindow.OpenAsync($"Paste target {Guid.NewGuid():N}", kind);
            await Foreground.ActivateAsync(target.Handle, "the paste target");
            await PasteAsync(target, _third);
            await Poll.UntilAsync(() => clipList.IsSelected(_second), "the clip list to follow the sequence");
            await PasteAsync(target, _third + _second);
            await PasteAsync(target, _third + _second + _first);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task OneClipSelected_FirstUseGoesUpToTheTop()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            // Lesson 2 of the manual: pick the oldest clip, and Power Paste works its way up
            var clipList = await CopyThreeClipsAsync();
            await SelectAndWaitForCopyAsync(clipList, _first);

            clipList.ClickToolbarButton("PowerPaste");
            await WaitForPowerPasteToArmAsync();

            using var target = await TestAppWindow.OpenAsync($"Paste target {Guid.NewGuid():N}");
            await Foreground.ActivateAsync(target.Handle, "the paste target");
            await PasteAsync(target, _first);
            await PasteAsync(target, _first + _second);
            await PasteAsync(target, _first + _second + _third);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task TwoClipsSelected_PastesThoseInOrder()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            var clipList = await CopyThreeClipsAsync(_goingDown);
            await SelectAndWaitForCopyAsync(clipList, _third);
            await clipList.AddToSelectionAsync(_second);
            await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process) && SystemClipboard.TryReadText(IntPtr.Zero) == _second,
                "ClipMate to copy the clip added to the selection");

            clipList.ClickToolbarButton("PowerPaste");
            await WaitForPowerPasteToArmAsync();

            using var target = await TestAppWindow.OpenAsync($"Paste target {Guid.NewGuid():N}");
            await Foreground.ActivateAsync(target.Handle, "the paste target");
            await PasteAsync(target, _third);
            await PasteAsync(target, _third + _second);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task Explode_OneClip_PastesEachFragment()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync("preferences.power_paste_explode=true", _goingDown);
            var clipList = new ClipListPage(appFixture.Process);

            var fragments = new[] { "alpha" + Guid.NewGuid().ToString("N")[..6], "beta", "gamma" };
            await CopyUntilListedAsync(clipList, string.Join(", ", fragments));
            await SelectAndWaitForCopyAsync(clipList, fragments[0]);

            clipList.ClickToolbarButton("PowerPaste");
            await WaitForPowerPasteToArmAsync();

            // The caption shows the Fragment waiting, as ClipMate 7's does, until the last one is pasted
            await Poll.UntilAsync(() => clipList.Title?.EndsWith($" [Fragment [{fragments[0]}]]", StringComparison.Ordinal) == true, "the first Fragment in the caption");

            using var target = await TestAppWindow.OpenAsync($"Paste target {Guid.NewGuid():N}");
            await Foreground.ActivateAsync(target.Handle, "the paste target");
            await PasteAsync(target, fragments[0]);
            await Poll.UntilAsync(() => clipList.Title?.EndsWith($" [Fragment [{fragments[1]}]]", StringComparison.Ordinal) == true, "the second Fragment in the caption");
            await PasteAsync(target, fragments[0] + fragments[1]);
            await PasteAsync(target, fragments[0] + fragments[1] + fragments[2]);

            // The sequence runs on to the end of the collection; stopping it takes the Fragment out of the caption
            clipList.ClickToolbarButton("PowerPaste");
            await Poll.UntilAsync(() => clipList.Title == "ClipMate - Clipboard Manager", "the caption without a Fragment once Power Paste stops");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task PastingFast_EachPasteAdvances() => await PasteThreeClipsFastAsync(_goingDown);

    [Test]
    public async Task PastingFast_WithClipsHiddenFromOtherPrograms_EachPasteAdvances() =>
        await PasteThreeClipsFastAsync(_goingDown, "preferences.power_paste_hide_from_other_programs=true");

    private async Task PasteThreeClipsFastAsync(params string[] settings)
    {
        var backup = SystemClipboard.Backup();
        try
        {
            var clipList = await CopyThreeClipsAsync(settings);
            await SelectAndWaitForCopyAsync(clipList, _third);

            clipList.ClickToolbarButton("PowerPaste");
            await WaitForPowerPasteToArmAsync();

            using var target = await TestAppWindow.OpenAsync($"Paste target {Guid.NewGuid():N}");
            await Foreground.ActivateAsync(target.Handle, "the paste target");
            await PasteAsync(target, _third);
            await PasteAsync(target, _third + _second, _fastPause);
            await PasteAsync(target, _third + _second + _first, _fastPause);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task ClickingAClipOutsideTheSequence_CancelsAndPastesThePickedClip() =>
        await PasteThenClickAClipOutsideTheSequenceAsync(_goingDown);

    [Test]
    public async Task ClickingAClipOutsideTheSequence_JustBeforeTheNextClipComesUp_CancelsAndPastesThePickedClip() =>
        await PasteThenClickAClipOutsideTheSequenceAsync(_goingDown, _slowToMoveOn);

    private async Task PasteThenClickAClipOutsideTheSequenceAsync(params string[] settings)
    {
        var backup = SystemClipboard.Backup();
        try
        {
            var clipList = await CopyThreeClipsAsync(settings);
            await SelectAndWaitForCopyAsync(clipList, _third);
            await clipList.AddToSelectionAsync(_second);
            await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process) && SystemClipboard.TryReadText(IntPtr.Zero) == _second,
                "ClipMate to copy the clip added to the selection");

            clipList.ClickToolbarButton("PowerPaste");
            await WaitForPowerPasteToArmAsync();

            using var target = await TestAppWindow.OpenAsync($"Paste target {Guid.NewGuid():N}");
            await Foreground.ActivateAsync(target.Handle, "the paste target");
            await PasteAsync(target, _third);

            // The second clip is waiting. Picking the first, which isn't in the sequence, cancels Power Paste as ClipMate 7's
            // clip list does, and the picked clip is copied as any pick is: the next paste is that clip.
            await clipList.SelectAsync(_first);
            await Poll.UntilAsync(() => SystemClipboard.GetOwnerWindowTitle() != "ClipMate.PowerPaste" && SystemClipboard.IsOwnedBy(appFixture.Process) &&
                                        SystemClipboard.TryReadText(IntPtr.Zero) == _first,
                "Power Paste to cancel and the picked clip to be copied");

            await Foreground.ActivateAsync(target.Handle, "the paste target");
            await PasteAsync(target, _third + _first);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task TurnedOnRightAfterPicking_StaysOn()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            var clipList = await CopyThreeClipsAsync(_goingDown);

            // A person picks a clip and clicks the button straight away, without waiting for the pick's own copy
            await clipList.SelectAsync(_second);
            clipList.ClickToolbarButton("PowerPaste");
            await WaitForPowerPasteToArmAsync();

            using var target = await TestAppWindow.OpenAsync($"Paste target {Guid.NewGuid():N}");
            await Foreground.ActivateAsync(target.Handle, "the paste target");
            await PasteAsync(target, _second);
            await PasteAsync(target, _second + _first);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    private static async Task WaitForPowerPasteToArmAsync() =>
        await Poll.UntilAsync(() => SystemClipboard.GetOwnerWindowTitle() == "ClipMate.PowerPaste", "Power Paste to arm the first clip");

    private static async Task SelectAndWaitForCopyAsync(ClipListPage clipList, string text)
    {
        await clipList.SelectAsync(text);

        // The newest clip is already the current row and is copied by capture, so wait for the text, not for a new copy
        await Poll.UntilAsync(() => SystemClipboard.TryReadText(IntPtr.Zero)?.Contains(text, StringComparison.Ordinal) == true, "the picked clip on the clipboard");
    }

    private async Task<ClipListPage> CopyThreeClipsAsync(params string[] settings)
    {
        await appFixture.StartAppAsync(settings);
        var clipList = new ClipListPage(appFixture.Process);
        foreach (var item in new[] { _first, _second, _third })
            await CopyUntilListedAsync(clipList, item);

        return clipList;
    }

    private static async Task CopyUntilListedAsync(ClipListPage clipList, string text)
    {
        // A copy made while the previous one is still being captured can be missed; copy again until it is listed
        var attempt = 0;
        await Poll.UntilAsync(() =>
        {
            if (attempt++ % 10 == 0)
                SystemClipboard.OnSta(() => Clipboard.SetText(text));

            return clipList.FindRow(text[..Math.Min(text.Length, 12)]);
        }, $"\"{text}\" in the clip list");
    }

    private static async Task PasteAsync(TestAppWindow target, string expectedText, TimeSpan? pause = null)
    {
        // A person's pace, not a wait for ClipMate
#pragma warning disable RS0030
        await Task.Delay(pause ?? _pause);
#pragma warning restore RS0030
        Foreground.PressKeys(Foreground.VkControl, _vkV);

        try
        {
            await Poll.UntilAsync(() => target.ReadText() == expectedText, $"\"{expectedText}\" in the paste target", TimeSpan.FromSeconds(4));
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException($"Expected \"{expectedText}\" in the paste target, found \"{target.ReadText()}\"");
        }
    }
}
