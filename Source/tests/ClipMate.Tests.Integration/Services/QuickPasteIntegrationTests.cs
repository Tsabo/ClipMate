using Windows.Win32.Foundation;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for QuickPaste functionality with real service interactions.
/// </summary>
public class QuickPasteIntegrationTests : IntegrationTestBase
{
    private IConfigurationService _configurationService = null!;
    private IMessenger _messenger = null!;
    private Mock<IClipboardService> _mockClipboardService = null!;
    private Mock<IWin32InputInterop> _mockWin32 = null!;
    private QuickPasteService _quickPasteService = null!;
    private ClipMateConfiguration _testConfig = null!;

    [Before(Test)]
    public async Task SetupQuickPasteTestAsync()
    {
        await SetupAsync();

        _mockWin32 = new Mock<IWin32InputInterop>();
        _mockClipboardService = new Mock<IClipboardService>();
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
        var mockLogger = Mock.Of<ILogger<ConfigurationService>>();
        _configurationService = new ConfigurationService(tempDir, mockLogger);

        // Manually set the configuration since we're not loading from file
        typeof(ConfigurationService)
            .GetProperty("Configuration")!
            .SetValue(_configurationService, _testConfig);

        var quickPasteLogger = Mock.Of<ILogger<QuickPasteService>>();

        _quickPasteService = new QuickPasteService(
            _mockWin32.Object,
            _mockClipboardService.Object,
            _configurationService,
            _messenger,
            Mock.Of<IMacroExecutionService>(),
            Mock.Of<ITemplateService>(),
            Mock.Of<IDialogService>(),
            quickPasteLogger);
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
    public async Task QuickPaste_TargetLock_PreventstargetUpdates()
    {
        // Arrange
        _quickPasteService.SetTargetLock(true);

        // Act
        _quickPasteService.UpdateTarget();

        // Assert - Target should remain null because it's locked
        var target = _quickPasteService.GetCurrentTarget();
        await Assert.That(target).IsNull();
    }

    [Test]
    public async Task QuickPaste_FormattingStringSelection_PersistsAcrossOperations()
    {
        // Arrange
        var excelFormat = _testConfig.Preferences.QuickPasteFormattingStrings
            .First(p => p.Title == "Excel - Fill Column");

        // Act
        _quickPasteService.SelectFormattingString(excelFormat);

        // Assert
        var selected = _quickPasteService.GetSelectedFormattingString();
        await Assert.That(selected).IsEqualTo(excelFormat);
        await Assert.That(selected!.Preamble).IsEqualTo("^{HOME}^@{DOWN}");
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
        var result = await _quickPasteService.PasteClipAsync(clip);

        // Assert - Should fail because no target is set
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task QuickPaste_PasteClip_ThrowsArgumentNullException_WhenClipIsNull()
    {
        // Act & Assert
        await Assert.That(async () => await _quickPasteService.PasteClipAsync(null!))
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
    public async Task QuickPaste_TargetString_ReturnsEmptyWhenNotSet()
    {
        // Act
        var targetString = _quickPasteService.GetCurrentTargetString();

        // Assert
        await Assert.That(targetString).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task QuickPaste_UpdateTarget_DoesNotThrowWhenNoWindow()
    {
        // Arrange - No foreground window
        _mockWin32.Setup(w => w.GetForegroundWindow()).Returns(new HWND(IntPtr.Zero));
        _quickPasteService.SetTargetLock(false);

        // Act
        _quickPasteService.UpdateTarget();

        // Assert - Should not throw, target remains null
        var target = _quickPasteService.GetCurrentTarget();
        await Assert.That(target).IsNull();
    }
}
