using Windows.Win32.Foundation;
using ClipMate.Core.Models.Configuration;
using ClipMate.Platform.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for QuickPasteService auto-targeting functionality.
/// </summary>
public partial class QuickPasteServiceTests
{
    [Test]
    public async Task UpdateTarget_ChecksAutoTargetingEnabledConfiguration()
    {
        // Arrange
        var mockConfig = CreateMockConfigurationService();
        A.CallTo(() => mockConfig.Configuration)
            .Returns(new ClipMateConfiguration
            {
                Preferences = new PreferencesConfiguration
                {
                    QuickPasteAutoTargetingEnabled = false,
                    QuickPasteFormattingStrings =
                    [
                        new QuickPasteFormattingString { Title = "Default", PasteKeystrokes = "^v", TitleTrigger = "*" },
                    ],
                    QuickPasteGoodTargets = ["NOTEPAD:EDIT"],
                    QuickPasteBadTargets = ["CLIPMATE:"],
                },
            });

        var mockWin32 = CreateMockWin32();
        A.CallTo(() => mockWin32.GetForegroundWindow())
            .Returns(new HWND(12345));

        var service = new QuickPasteService(
            mockWin32,
            CreateMockClipboardService(),
            mockConfig,
            CreateMockMessenger(),
            CreateMockMacroExecutionService(),
            CreateMockTemplateService(),
            CreateMockDialogService(),
            CreateMockLogger());

        // Act
        service.UpdateTarget();

        // Assert - GetForegroundWindow should NOT be called since auto-targeting is disabled
        A.CallTo(() => mockWin32.GetForegroundWindow()).MustNotHaveHappened();
        await Assert.That(service.GetCurrentTarget()).IsNull();
    }

    [Test]
    public void UpdateTarget_CallsGetForegroundWindow_WhenAutoTargetingEnabled()
    {
        // Arrange
        var mockConfig = CreateMockConfigurationService();
        A.CallTo(() => mockConfig.Configuration)
            .Returns(new ClipMateConfiguration
            {
                Preferences = new PreferencesConfiguration
                {
                    QuickPasteAutoTargetingEnabled = true,
                    QuickPasteFormattingStrings =
                    [
                        new QuickPasteFormattingString { Title = "Default", PasteKeystrokes = "^v", TitleTrigger = "*" },
                    ],
                    QuickPasteGoodTargets = ["NOTEPAD:EDIT"],
                    QuickPasteBadTargets = ["CLIPMATE:"],
                },
            });

        var mockWin32 = CreateMockWin32();

        // Return null HWND (no window) to keep test simple
        A.CallTo(() => mockWin32.GetForegroundWindow())
            .Returns(new HWND(nint.Zero));

        var service = new QuickPasteService(
            mockWin32,
            CreateMockClipboardService(),
            mockConfig,
            CreateMockMessenger(),
            CreateMockMacroExecutionService(),
            CreateMockTemplateService(),
            CreateMockDialogService(),
            CreateMockLogger());

        // Act
        service.UpdateTarget();

        // Assert - GetForegroundWindow should be called when auto-targeting is enabled
        A.CallTo(() => mockWin32.GetForegroundWindow()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public void UpdateTarget_RespectsTargetLockEvenWhenAutoTargetingEnabled()
    {
        // Arrange
        var mockConfig = CreateMockConfigurationService();
        A.CallTo(() => mockConfig.Configuration)
            .Returns(new ClipMateConfiguration
            {
                Preferences = new PreferencesConfiguration
                {
                    QuickPasteAutoTargetingEnabled = true,
                    QuickPasteFormattingStrings =
                    [
                        new QuickPasteFormattingString { Title = "Default", PasteKeystrokes = "^v", TitleTrigger = "*" },
                    ],
                    QuickPasteGoodTargets = ["NOTEPAD:EDIT"],
                    QuickPasteBadTargets = ["CLIPMATE:"],
                },
            });

        var mockWin32 = CreateMockWin32();
        var service = new QuickPasteService(
            mockWin32,
            CreateMockClipboardService(),
            mockConfig,
            CreateMockMessenger(),
            CreateMockMacroExecutionService(),
            CreateMockTemplateService(),
            CreateMockDialogService(),
            CreateMockLogger());

        // Lock the target
        service.SetTargetLock(true);

        // Act
        service.UpdateTarget();

        // Assert - GetForegroundWindow should NOT be called since target is locked
        A.CallTo(() => mockWin32.GetForegroundWindow()).MustNotHaveHappened();
    }
}
