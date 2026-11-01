using Windows.Win32.Foundation;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for QuickPaste functionality with real service interactions.
/// </summary>
public class QuickPasteIntegrationTests : IntegrationTestBase
{
    private IConfigurationService _configurationService = null!;
    private IForegroundWindowMonitor _foregroundMonitor = null!;
    private IMessenger _messenger = null!;
    private ICurrentClipWriter _mockCurrentClipWriter = null!;
    private IWin32InputInterop _mockWin32 = null!;
    private PasteTarget _pasteTarget = null!;
    private PasteTargetTracker _pasteTargetTracker = null!;
    private QuickPasteService _quickPasteService = null!;
    private ClipMateConfiguration _testConfig = null!;

    [Before(Test)]
    public async Task SetupQuickPasteTestAsync()
    {
        await SetupAsync();

        _mockWin32 = A.Fake<IWin32InputInterop>();
        _mockCurrentClipWriter = A.Fake<ICurrentClipWriter>();
        _messenger = WeakReferenceMessenger.Default;

        // Create test configuration with realistic QuickPaste settings
        _testConfig = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                QuickPasteAutoTargetingEnabled = true,
                QuickPastePasteOnEnter = true,
                QuickPastePasteOnDoubleClick = true,
                QuickPasteFormattingStrings =
                [
                    new QuickPasteFormattingString
                    {
                        Title = "Default - Ctrl+V",
                        Preamble = "",
                        PasteKeystrokes = "^v",
                        Postamble = "",
                        TitleTrigger = "*",
                    },

                    new QuickPasteFormattingString
                    {
                        Title = "Excel - Fill Column",
                        Preamble = "^{HOME}^@{DOWN}",
                        PasteKeystrokes = "^v",
                        Postamble = "{DOWN}",
                        TitleTrigger = "Microsoft Excel",
                    },

                    new QuickPasteFormattingString
                    {
                        Title = "Notepad with Date",
                        Preamble = "#CURRENTDATE# - ",
                        PasteKeystrokes = "^v",
                        Postamble = "{ENTER}",
                        TitleTrigger = "Notepad",
                    },
                ],
                QuickPasteGoodTargets =
                [
                    "NOTEPAD:EDIT",
                    "CODE:CHROME_WIDGETWIN_1",
                    "DEVENV:HWNDWRAPPER",
                ],
                QuickPasteBadTargets =
                [
                    "CLIPMATE:WINDOW",
                    "EXPLORER:CABINETWCLASS",
                ],
            },
        };

        var tempDir = Path.Combine(Path.GetTempPath(), "ClipMateTests", Guid.NewGuid().ToString());
        var mockLogger = A.Fake<ILogger<ConfigurationService>>();
        _configurationService = new ConfigurationService(tempDir, mockLogger);

        // Manually set the configuration since we're not loading from file
        typeof(ConfigurationService)
            .GetProperty("Configuration")!
            .SetValue(_configurationService, _testConfig);

        var quickPasteLogger = A.Fake<ILogger<QuickPasteService>>();

        _foregroundMonitor = A.Fake<IForegroundWindowMonitor>();
        _pasteTarget = new PasteTarget();
        _pasteTargetTracker = new PasteTargetTracker(
            _mockWin32,
            _foregroundMonitor,
            _pasteTarget,
            _configurationService,
            _messenger,
            A.Fake<ILogger<PasteTargetTracker>>());

        _quickPasteService = new QuickPasteService(
            _mockWin32,
            _pasteTarget,
            _mockCurrentClipWriter,
            A.Fake<IDatabases>(),
            _configurationService,
            _messenger,
            A.Fake<IMacroExecutionService>(),
            A.Fake<ITemplateService>(),
            A.Fake<IDialogService>(),
            quickPasteLogger);
    }

    [After(Test)]
    public void CleanupQuickPasteTest()
    {
        _quickPasteService?.Dispose();
        _pasteTargetTracker?.Dispose();
    }

    [Test]
    public async Task QuickPaste_SelectsDefaultFormattingString_OnInitialization()
    {
        // Assert
        var selected = _quickPasteService.GetSelectedFormattingString();
        await Assert.That(selected).IsNotNull();
        await Assert.That(selected!.Title).IsEqualTo("Default - Ctrl+V");
        await Assert.That(selected.TitleTrigger).IsEqualTo("*");
    }

    [Test]
    public async Task QuickPaste_TargetLock_PreventsTargetUpdates()
    {
        // Arrange: Notepad, a suitable window, is about to come to the front while the target is locked
        var notepad = ArrangeNotepadWindow();
        _pasteTargetTracker.Start();
        _pasteTargetTracker.SetLocked(true);

        // Act: the same foreground change while locked and after unlocking
        _foregroundMonitor.ForegroundChanged += Raise.FreeForm<Action<HWND>>.With(notepad);
        var targetWhileLocked = _pasteTarget.Window;
        _pasteTargetTracker.SetLocked(false);
        _foregroundMonitor.ForegroundChanged += Raise.FreeForm<Action<HWND>>.With(notepad);

        // Assert
        await Assert.That(targetWhileLocked).IsNull();
        await Assert.That(_pasteTarget.Window?.TargetString).IsEqualTo("NOTEPAD:");
    }

    [Test]
    public async Task QuickPaste_FormattingStringSelection_PersistsAcrossOperations()
    {
        // Arrange
        var excelFormat = _testConfig.Preferences.QuickPasteFormattingStrings
            .First(p => p.Title == "Excel - Fill Column");

        // Act
        _quickPasteService.SelectFormattingString(excelFormat);

        // Assert - Compare by properties since QuickPasteFormattingString doesn't implement value equality
        var selected = _quickPasteService.GetSelectedFormattingString();
        await Assert.That(selected).IsNotNull();
        await Assert.That(selected!.Title).IsEqualTo(excelFormat.Title);
        await Assert.That(selected!.Preamble).IsEqualTo(excelFormat.Preamble);
        await Assert.That(selected!.PasteKeystrokes).IsEqualTo(excelFormat.PasteKeystrokes);
        await Assert.That(selected!.Postamble).IsEqualTo(excelFormat.Postamble);
        await Assert.That(selected!.TitleTrigger).IsEqualTo(excelFormat.TitleTrigger);
    }

    [Test]
    public async Task QuickPaste_GoBackState_TogglesCorrectly()
    {
        // Arrange - Initially false (default value)
        await Assert.That(_quickPasteService.GetGoBackState()).IsFalse();

        // Act - Toggle on
        _quickPasteService.SetGoBackState(true);
        await Assert.That(_quickPasteService.GetGoBackState()).IsTrue();

        // Act - Toggle back off
        _quickPasteService.SetGoBackState(false);
        await Assert.That(_quickPasteService.GetGoBackState()).IsFalse();
    }

    [Test]
    public async Task QuickPaste_SequenceReset_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw
        _quickPasteService.ResetSequence();

        // Verify service is still functional after reset
        var selected = _quickPasteService.GetSelectedFormattingString();
        await Assert.That(selected).IsNotNull();
    }

    [Test]
    public async Task QuickPaste_PasteClip_FailsWhenNoTargetSet()
    {
        // Arrange
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Integration test content",
            CapturedAt = DateTimeOffset.Now,
            ContentHash = "TestHash",
        };

        // Act
        var result = await _quickPasteService.PasteClipAsync("db", clip);

        // Assert - Should fail because no target is set
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task QuickPaste_PasteClip_ThrowsArgumentNullException_WhenClipIsNull()
    {
        // Act & Assert
        await Assert.That(async () => await _quickPasteService.PasteClipAsync("db", null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task QuickPaste_ConfigurationReload_SelectsNewDefaultFormat()
    {
        // Arrange - Change the default format in configuration
        _testConfig.Preferences.QuickPasteFormattingStrings.Clear();
        _testConfig.Preferences.QuickPasteFormattingStrings.Add(new QuickPasteFormattingString
        {
            Title = "New Default",
            Preamble = "",
            PasteKeystrokes = "^v",
            Postamble = "",
            TitleTrigger = "*",
        });

        // Act - Trigger configuration change event
        _messenger.Send(new QuickPasteConfigurationChangedEvent());

        // Wait a bit for async event handling
        await Task.Delay(100);

        // Assert - Should have selected the new default
        var selected = _quickPasteService.GetSelectedFormattingString();
        await Assert.That(selected).IsNotNull();
        await Assert.That(selected!.Title).IsEqualTo("New Default");
    }

    [Test]
    public async Task QuickPaste_MultipleFormattingStrings_CanSwitchBetweenThem()
    {
        // Arrange
        var formats = _testConfig.Preferences.QuickPasteFormattingStrings;

        // Act & Assert - Switch through all formats
        foreach (var item in formats)
        {
            _quickPasteService.SelectFormattingString(item);
            var selected = _quickPasteService.GetSelectedFormattingString();
            await Assert.That(selected).IsEqualTo(item);
            await Assert.That(selected!.Title).IsEqualTo(item.Title);
        }
    }

    [Test]
    public async Task QuickPaste_StartTargetTracking_DoesNotThrowWhenNoWindow()
    {
        // Arrange - No foreground window
        A.CallTo(() => _mockWin32.GetForegroundWindow()).Returns(new HWND(IntPtr.Zero));

        // Act
        _pasteTargetTracker.Start();

        // Assert - Should not throw, target remains null
        await Assert.That(_pasteTarget.Window).IsNull();
    }

    /// <summary>
    /// A Notepad window without class name or title; "NOTEPAD:" on the GOOD list makes it suitable all the same.
    /// </summary>
    private HWND ArrangeNotepadWindow()
    {
        const uint processId = 4242;
        var notepad = new HWND(0x1234);
        uint ignored;
        A.CallTo(() => _mockWin32.GetWindowThreadProcessId(notepad, out ignored)).Returns(1u).AssignsOutAndRefParameters(processId);
        A.CallTo(() => _mockWin32.GetProcessName(processId)).Returns("notepad");
        _testConfig.Preferences.QuickPasteGoodTargets = [.. _testConfig.Preferences.QuickPasteGoodTargets, "NOTEPAD:"];

        return notepad;
    }
}
