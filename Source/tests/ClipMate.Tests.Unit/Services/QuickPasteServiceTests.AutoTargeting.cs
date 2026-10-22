using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for QuickPasteService auto-targeting: following the foreground window, ClipMate 7's suitability rules,
/// the target lock and the window-stack fallback, on a simulated desktop.
/// </summary>
public partial class QuickPasteServiceTests
{
    [Test]
    public async Task StartTargetTracking_StartsTheMonitorAndTakesTheForegroundWindow()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var notepad = desktop.Open("notepad", "Notepad", "Untitled - Notepad");
        desktop.Activate(notepad);

        // Act
        var service = CreateTrackingService(desktop);

        // Assert
        await Assert.That(desktop.IsStarted).IsTrue();
        await Assert.That(service.GetCurrentTargetString()).IsEqualTo("NOTEPAD:NOTEPAD");
    }

    [Test]
    public async Task ForegroundChange_ToASuitableWindow_BecomesTheTargetWhileClipMateIsInTheBackground()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var notepad = desktop.Open("notepad", "Notepad", "Untitled - Notepad");
        var word = desktop.Open("winword", "OpusApp", "Report - Word");
        var service = CreateTrackingService(desktop);

        // Act
        desktop.Activate(notepad);
        desktop.Activate(word);

        // Assert
        await Assert.That(service.GetCurrentTargetString()).IsEqualTo("WINWORD:OPUSAPP");
        await Assert.That(service.GetCurrentTarget()?.WindowTitle).IsEqualTo("Report - Word");
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
        var service = CreateTrackingService(desktop);

        // Act
        desktop.Activate(excel);
        desktop.Activate(word);
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(service.GetCurrentTargetString()).IsEqualTo("WINWORD:OPUSAPP");
    }

    [Test]
    public async Task ForegroundChange_ToClipMate_NeverMakesClipMateTheTarget()
    {
        // Arrange: ClipMate's own windows are recognised by process, even when it is on the GOOD list
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var service = CreateTrackingService(desktop, new PreferencesConfiguration { QuickPasteGoodTargets = ["CLIPMATE:"] });

        // Act
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(service.GetCurrentTarget()).IsNull();
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
        CreateTrackingService(desktop, messenger: messenger);

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
        var service = CreateTrackingService(desktop, new PreferencesConfiguration { QuickPasteAutoTargetingEnabled = false });

        // Act
        desktop.Activate(word);

        // Assert
        await Assert.That(service.GetCurrentTarget()).IsNull();
    }

    [Test]
    public async Task ForegroundChange_WhileTheTargetIsLocked_KeepsTheLockedTarget()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var excel = desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var service = CreateTrackingService(desktop);
        desktop.Activate(excel);
        service.SetTargetLock(true);

        // Act
        desktop.Activate(word);

        // Assert
        await Assert.That(service.GetCurrentTargetString()).IsEqualTo("EXCEL:XLMAIN");
    }

    [Test]
    public async Task SetTargetLock_Unlocking_AdoptsTheLastSuitableWindowSeenWhileLocked()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var excel = desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var service = CreateTrackingService(desktop);
        desktop.Activate(excel);
        service.SetTargetLock(true);
        desktop.Activate(word);
        desktop.Activate(clipMate);

        // Act
        service.SetTargetLock(false);

        // Assert
        await Assert.That(service.GetCurrentTargetString()).IsEqualTo("WINWORD:OPUSAPP");
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
        var service = CreateTrackingService(desktop);
        desktop.Activate(notepad);
        desktop.Activate(word);

        // Act
        desktop.Close(word);
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(service.GetCurrentTarget()?.WindowTitle).IsEqualTo("Untitled - Notepad");
    }

    [Test]
    public async Task ClipMateOpens_AfterTheTargetWasHidden_TakesTheNextSuitableWindowDownTheStack()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var excel = desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var service = CreateTrackingService(desktop);
        desktop.Activate(excel);
        desktop.Activate(word);

        // Act
        word.IsVisible = false;
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(service.GetCurrentTargetString()).IsEqualTo("EXCEL:XLMAIN");
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
        var service = CreateTrackingService(desktop);
        desktop.Activate(word);
        desktop.Activate(excel);
        desktop.Activate(word);
        desktop.Activate(taskbar);

        // Act
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(service.GetCurrentTargetString()).IsEqualTo("WINWORD:OPUSAPP");
    }

    [Test]
    public async Task ClipMateOpens_AfterTheTargetClosed_WithNothingSuitableLeft_ClearsTheTarget()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        desktop.Open("explorer", "Shell_TrayWnd", "Taskbar");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var service = CreateTrackingService(desktop);
        desktop.Activate(word);

        // Act
        desktop.Close(word);
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(service.GetCurrentTarget()).IsNull();
    }

    [Test]
    public async Task ClipMateOpens_AfterTheLockedTargetClosed_KeepsTheLockedTarget()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        desktop.Open("excel", "XLMAIN", "Book1 - Excel");
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var service = CreateTrackingService(desktop);
        desktop.Activate(word);
        service.SetTargetLock(true);

        // Act
        desktop.Close(word);
        desktop.Activate(clipMate);

        // Assert
        await Assert.That(service.GetCurrentTargetString()).IsEqualTo("WINWORD:OPUSAPP");
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
        preferences.QuickPasteBadTargets.Add(":MSCTFIME UI");
        var desktop = new FakeDesktop();
        var notepad = desktop.Open("notepad", "Notepad", "Untitled - Notepad");
        var candidate = desktop.Open(processName, className, title);
        var service = CreateTrackingService(desktop, preferences);
        desktop.Activate(notepad);

        // Act
        desktop.Activate(candidate);

        // Assert
        var expected = expectedSuitable
            ? $"{processName}:{className}".ToUpperInvariant()
            : "NOTEPAD:NOTEPAD";

        await Assert.That(service.GetCurrentTargetString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ForegroundChange_UsesTheEditedBadTitlesList()
    {
        // Arrange
        var desktop = new FakeDesktop();
        var secret = desktop.Open("someapp", "SomeClass", "Secret window");
        var ime = desktop.Open("otherapp", "OtherClass", "Default IME");
        var service = CreateTrackingService(desktop, new PreferencesConfiguration { QuickPasteBadTitles = ["SECRET"] });

        // Act
        desktop.Activate(ime);
        desktop.Activate(secret);

        // Assert
        await Assert.That(service.GetCurrentTargetString()).IsEqualTo("OTHERAPP:OTHERCLASS");
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

    [Test]
    public async Task PasteClipAsync_FocusesTheTrackedTarget_WithoutLookingForANewOne()
    {
        // Arrange: Word is the target, then ClipMate is in front when the clip is pasted
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        var service = CreateTrackingService(desktop);
        desktop.Activate(word);
        desktop.Activate(clipMate);
        service.SetGoBackState(false);

        // Act
        var pasted = await service.PasteClipAsync(CreateTestClip());

        // Assert
        await Assert.That(pasted).IsTrue();
        await Assert.That(desktop.ForegroundRequests).IsEquivalentTo(new[] { word.Handle });
    }

    private QuickPasteService CreateTrackingService(FakeDesktop desktop, PreferencesConfiguration? preferences = null,
        IMessenger? messenger = null)
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration)
            .Returns(new ClipMateConfiguration { Preferences = preferences ?? new PreferencesConfiguration() });

        var service = new QuickPasteService(
            desktop,
            desktop,
            CreateMockClipboardService(),
            configurationService,
            messenger ?? new StrongReferenceMessenger(),
            CreateMockMacroExecutionService(),
            CreateMockTemplateService(),
            CreateMockDialogService(),
            CreateMockLogger());

        service.StartTargetTracking();
        return service;
    }
}
