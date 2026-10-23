using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Auto-targeting: following the foreground window, ClipMate 7's suitability rules, the target lock and the
/// window-stack fallback, on a simulated desktop.
/// </summary>
public class PasteTargetTrackerTests
{
    [Test]
    public async Task Start_StartsTheMonitorAndTakesTheForegroundWindow()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var notepad = desktop.Open("notepad", "Notepad", "Untitled - Notepad");
        desktop.Activate(notepad);

        // Act
        var target = StartTracking(desktop).Target;

        // Assert
        await Assert.That(desktop.IsStarted).IsTrue();
        await Assert.That(target.Window?.TargetString).IsEqualTo("NOTEPAD:NOTEPAD");
    }

    [Test]
    public async Task ForegroundChange_ToASuitableWindow_BecomesTheTargetWhileClipMateIsInTheBackground()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var notepad = desktop.Open("notepad", "Notepad", "Untitled - Notepad");
        var word = desktop.Open("winword", "OpusApp", "Report - Word");
        var target = StartTracking(desktop).Target;

        // Act
        desktop.Activate(notepad);
        desktop.Activate(word);

        // Assert
        await Assert.That(target.Window?.TargetString).IsEqualTo("WINWORD:OPUSAPP");
        await Assert.That(target.Window?.WindowTitle).IsEqualTo("Report - Word");
        await Assert.That(target.Window?.Handle).IsEqualTo(word.Handle);
    }

    [Test]
    public async Task ExcelThenWordThenClipMate_TheTargetIsWord()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var excel = desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        desktop.Activate(clipMate);
        var target = StartTracking(desktop).Target;

        // Act
        desktop.Activate(excel);
        desktop.Activate(word);
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(target.Window?.TargetString).IsEqualTo("WINWORD:OPUSAPP");
    }

    [Test]
    public async Task ForegroundChange_ToClipMate_NeverMakesClipMateTheTarget()
    {
        // Arrange: ClipMate's own windows are recognised by process, even when it is on the GOOD list
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var target = StartTracking(desktop, new PreferencesConfiguration { QuickPasteGoodTargets = ["CLIPMATE:"] }).Target;

        // Act
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(target.Window).IsNull();
    }

    [Test]
    public async Task ForegroundChange_RaisesTargetChanged_OnlyWhenTheTargetChanges()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var messenger = new StrongReferenceMessenger();
        var changes = 0;
        messenger.Register<QuickPasteTargetChangedEvent>(this, (_, _) => changes++);
        StartTracking(desktop, messenger: messenger);

        // Act: Word, ClipMate and Word again
        desktop.Activate(word);
        desktop.Activate(clipMate);
        desktop.Activate(word);

        // Assert
        await Assert.That(changes).IsEqualTo(1);
    }

    [Test]
    public async Task ForegroundChange_WhenAutoTargetingIsDisabled_NeverSetsATarget()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var target = StartTracking(desktop, new PreferencesConfiguration { QuickPasteAutoTargetingEnabled = false }).Target;

        // Act
        desktop.Activate(word);

        // Assert
        await Assert.That(target.Window).IsNull();
    }

    [Test]
    public async Task SetLocked_UpdatesTheLockStateAndAnnouncesIt()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var messenger = new StrongReferenceMessenger();
        var refreshes = 0;
        messenger.Register<StateRefreshRequestedEvent>(this, (_, _) => refreshes++);
        var (tracker, target) = StartTracking(desktop, messenger: messenger);
        var lockedInitially = target.IsLocked;

        // Act
        tracker.SetLocked(true);

        // Assert
        await Assert.That(lockedInitially).IsFalse();
        await Assert.That(target.IsLocked).IsTrue();
        await Assert.That(refreshes).IsEqualTo(1);
    }

    [Test]
    public async Task ForegroundChange_WhileTheTargetIsLocked_KeepsTheLockedTarget()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var excel = desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var (tracker, target) = StartTracking(desktop);
        desktop.Activate(excel);
        tracker.SetLocked(true);

        // Act
        desktop.Activate(word);

        // Assert
        await Assert.That(target.Window?.TargetString).IsEqualTo("EXCEL:XLMAIN");
    }

    [Test]
    public async Task SetLocked_Unlocking_AdoptsTheLastSuitableWindowSeenWhileLocked()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var excel = desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var (tracker, target) = StartTracking(desktop);
        desktop.Activate(excel);
        tracker.SetLocked(true);
        desktop.Activate(word);
        desktop.Activate(clipMate);

        // Act
        tracker.SetLocked(false);

        // Assert
        await Assert.That(target.Window?.TargetString).IsEqualTo("WINWORD:OPUSAPP");
    }

    [Test]
    public async Task ClipMateOpens_AfterTheTargetClosed_TakesTheNextSuitableWindowDownTheStack()
    {
        // Arrange: below ClipMate, from the top: another ClipMate window, a hidden window, a cloaked window, the
        // taskbar (BAD list), an untitled window, then Notepad
        var desktop = new FakeDesktop();
        var notepad = desktop.Open("notepad", "Notepad", "Untitled - Notepad");
        desktop.Open("someapp", "SomeClass", string.Empty);
        desktop.Open("explorer", "Shell_TrayWnd", "Taskbar");
        desktop.Open("store", "ApplicationFrameWindow", "Settings").IsCloaked = true;
        desktop.Open("hiddenapp", "HiddenClass", "Hidden").IsVisible = false;
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        desktop.OpenClipMate("ClipBar");
        var clipMate = desktop.OpenClipMate();
        var target = StartTracking(desktop).Target;
        desktop.Activate(notepad);
        desktop.Activate(word);

        // Act
        desktop.Close(word);
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(target.Window?.WindowTitle).IsEqualTo("Untitled - Notepad");
    }

    [Test]
    public async Task ClipMateOpens_AfterTheTargetWasHidden_TakesTheNextSuitableWindowDownTheStack()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var excel = desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var target = StartTracking(desktop).Target;
        desktop.Activate(excel);
        desktop.Activate(word);

        // Act
        word.IsVisible = false;
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(target.Window?.TargetString).IsEqualTo("EXCEL:XLMAIN");
    }

    [Test]
    public async Task ClipMateOpens_WhileTheTargetIsStillShown_KeepsItEvenIfAnotherWindowIsAbove()
    {
        // Arrange: the taskbar was clicked after Word, so it sits between ClipMate and Word
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var excel = desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var taskbar = desktop.Open("explorer", "Shell_TrayWnd", "Taskbar");
        var target = StartTracking(desktop).Target;
        desktop.Activate(word);
        desktop.Activate(excel);
        desktop.Activate(word);
        desktop.Activate(taskbar);

        // Act
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(target.Window?.TargetString).IsEqualTo("WINWORD:OPUSAPP");
    }

    [Test]
    public async Task ClipMateOpens_AfterTheTargetClosed_WithNothingSuitableLeft_ClearsTheTarget()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        desktop.Open("explorer", "Shell_TrayWnd", "Taskbar");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var target = StartTracking(desktop).Target;
        desktop.Activate(word);

        // Act
        desktop.Close(word);
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(target.Window).IsNull();
    }

    [Test]
    public async Task ClipMateOpens_AfterTheLockedTargetClosed_KeepsTheLockedTarget()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var (tracker, target) = StartTracking(desktop);
        desktop.Activate(word);
        tracker.SetLocked(true);

        // Act
        desktop.Close(word);
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(target.Window?.TargetString).IsEqualTo("WINWORD:OPUSAPP");
    }

    [Test]
    [Arguments("excel", "XLMAIN", "", true)] // GOOD list wins over a missing title
    [Arguments("winword", "OpusApp", "Default IME", true)] // GOOD list wins over a bad title
    [Arguments("explorer", "CabinetWClass", "Documents", true)] // File Explorer stays allowed
    [Arguments("explorer", "Shell_TrayWnd", "Taskbar", false)] // BAD list
    [Arguments("ctfmon", "CiceroUIWndFrame", "CiceroUIWndFrame", false)] // CTFMON: is on the default BAD list
    [Arguments("clipmate", "TfrmClipMate", "ClipMate 7", false)] // CM7 itself
    [Arguments("someapp", "MSCTFIME UI x", "IME", false)] // per-part prefix match of ":MSCTFIME UI"
    [Arguments("someapp", "SomeClass", "", false)] // no title
    [Arguments("someapp", "SomeClass", "   ", false)] // blank title
    [Arguments("someapp", "SomeClass", "Default IME", false)] // bad title
    [Arguments("someapp", "SomeClass", "IntelliFormProxy 2", false)] // bad title prefix, any case
    [Arguments("someapp", "SomeClass", "My Default IME notes", true)] // a bad title only matches at the start
    [Arguments("someapp", "SomeClass", "Untitled - SomeApp", true)] // anything else
    public async Task ForegroundChange_AppliesTheSuitabilityRules(string processName, string className, string title,
        bool expectedSuitable)
    {
        // Arrange: the default lists, plus a class-prefix entry; Notepad is the target to begin with
        var preferences = new PreferencesConfiguration();
        preferences.QuickPasteBadTargets = [.. preferences.QuickPasteBadTargets, ":MSCTFIME UI"];
        var desktop = new FakeDesktop();
        var notepad = desktop.Open("notepad", "Notepad", "Untitled - Notepad");
        var candidate = desktop.Open(processName, className, title);
        var target = StartTracking(desktop, preferences).Target;
        desktop.Activate(notepad);

        // Act
        desktop.Activate(candidate);

        // Assert
        var expected = expectedSuitable
            ? $"{processName}:{className}".ToUpperInvariant()
            : "NOTEPAD:NOTEPAD";

        await Assert.That(target.Window?.TargetString).IsEqualTo(expected);
    }

    [Test]
    public async Task ForegroundChange_UsesTheEditedBadTitlesList()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var secret = desktop.Open("someapp", "SomeClass", "Secret window");
        var ime = desktop.Open("otherapp", "OtherClass", "Default IME");
        var target = StartTracking(desktop, new PreferencesConfiguration { QuickPasteBadTitles = ["SECRET"] }).Target;

        // Act
        desktop.Activate(ime);
        desktop.Activate(secret);

        // Assert
        await Assert.That(target.Window?.TargetString).IsEqualTo("OTHERAPP:OTHERCLASS");
    }

    [Test]
    public async Task ForegroundChange_AfterAListIsReplaced_UsesTheNewList()
    {
        // Arrange: the toolbar's "add to BAD targets" replaces the list once the rules were already in use
        var preferences = new PreferencesConfiguration();
        var desktop = new FakeDesktop();
        var notepad = desktop.Open("notepad", "Notepad", "Untitled - Notepad");
        var someApp = desktop.Open("someapp", "SomeClass", "Some App");
        var target = StartTracking(desktop, preferences).Target;
        desktop.Activate(notepad);

        // Act
        preferences.QuickPasteBadTargets = [.. preferences.QuickPasteBadTargets, "SOMEAPP:"];
        desktop.Activate(someApp);

        // Assert
        await Assert.That(target.Window?.TargetString).IsEqualTo("NOTEPAD:NOTEPAD");
    }

    [Test]
    public async Task DefaultPreferences_ListCtfmonAsBadAndTheCm7BadTitles()
    {
        // Act
        var preferences = new PreferencesConfiguration();

        // Assert
        await Assert.That(preferences.QuickPasteBadTargets).Contains("CTFMON:");
        await Assert.That(preferences.QuickPasteBadTitles).IsEquivalentTo(new[] { "INTELLIFORMPROXY", "DEFAULT IME" });
    }

    private static (PasteTargetTracker Tracker, PasteTarget Target) StartTracking(FakeDesktop desktop,
        PreferencesConfiguration? preferences = null, IMessenger? messenger = null)
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration)
            .Returns(new ClipMateConfiguration { Preferences = preferences ?? new PreferencesConfiguration() });

        var target = new PasteTarget();
        var tracker = new PasteTargetTracker(
            desktop,
            desktop,
            target,
            configurationService,
            messenger ?? new StrongReferenceMessenger(),
            A.Fake<ILogger<PasteTargetTracker>>());

        tracker.Start();
        return (tracker, target);
    }
}
