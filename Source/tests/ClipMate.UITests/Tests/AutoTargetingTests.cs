using System.Windows;
using System.Windows.Automation;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Auto-targeting follows the foreground window: the Paste Target is the last suitable window the user was in before
/// opening ClipMate, and a QuickPaste lands there. Two helper windows stand in for Excel and Word.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards. Switches the foreground
/// window, so leave the keyboard and mouse alone while it runs.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class AutoTargetingTests(AppFixture appFixture)
{
    private const string _mainWindowTitle = "ClipMate - Clipboard Manager";

    [Test]
    public async Task QuickPaste_AfterSwitchingFromOneWindowToAnother_PastesIntoTheLastOne()
    {
        var text = $"auto-target {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync();
            using var excel = await TargetWindow.OpenAsync($"UI-Test Excel {Guid.NewGuid():N}");
            using var word = await TargetWindow.OpenAsync($"UI-Test Word {Guid.NewGuid():N}");
            var clipList = new ClipListPage(appFixture.Process);
            SystemClipboard.OnSta(() => Clipboard.SetText(text));
            await clipList.WaitForRowAsync(text);

            // Act: ClipMate → Excel → Word → ClipMate, then QuickPaste the clip with ENTER
            var clipMate = FindMainWindow();
            await Foreground.ActivateAsync(clipMate, "ClipMate");
            await Foreground.ActivateAsync(excel.Handle, "the Excel stand-in");
            await Foreground.ActivateAsync(word.Handle, "the Word stand-in");
            await Foreground.ActivateAsync(clipMate, "ClipMate");
            await QuickPasteAsync(clipList, text);

            // Assert
            await Poll.UntilAsync(() => word.ReadText().Contains(text, StringComparison.Ordinal), "the clip in the Word stand-in");
            await Assert.That(excel.ReadText()).DoesNotContain(text);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task QuickPaste_AfterTheLastWindowClosed_PastesIntoTheNextWindowDownTheStack()
    {
        var text = $"auto-target fallback {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync();
            using var excel = await TargetWindow.OpenAsync($"UI-Test Excel {Guid.NewGuid():N}");
            using var word = await TargetWindow.OpenAsync($"UI-Test Word {Guid.NewGuid():N}");
            var clipList = new ClipListPage(appFixture.Process);
            SystemClipboard.OnSta(() => Clipboard.SetText(text));
            await clipList.WaitForRowAsync(text);

            // Act: Excel → Word, Word closes, then ClipMate opens and QuickPastes
            var clipMate = FindMainWindow();
            await Foreground.ActivateAsync(excel.Handle, "the Excel stand-in");
            await Foreground.ActivateAsync(word.Handle, "the Word stand-in");
            word.Close();
            await Foreground.ActivateAsync(clipMate, "ClipMate");
            await QuickPasteAsync(clipList, text);

            // Assert: Excel was just below Word in the window stack (Windows may also hand Excel the foreground when
            // Word closes; either way Excel has to be the target)
            await Poll.UntilAsync(() => excel.ReadText().Contains(text, StringComparison.Ordinal), "the clip in the Excel stand-in");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    private static async Task QuickPasteAsync(ClipListPage clipList, string text)
    {
        var row = await clipList.WaitForRowAsync(text);
        ((SelectionItemPattern)row.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        row.SetFocus();
        Foreground.PressKey(Foreground.VkReturn);
    }

    private nint FindMainWindow()
    {
        var window = AutomationElement.RootElement.FindFirst(TreeScope.Children, new AndCondition(
                         new PropertyCondition(AutomationElement.ProcessIdProperty, appFixture.Process.Id),
                         new PropertyCondition(AutomationElement.NameProperty, _mainWindowTitle)))
                     ?? throw new InvalidOperationException("ClipMate's main window was not found");

        return window.Current.NativeWindowHandle;
    }
}
