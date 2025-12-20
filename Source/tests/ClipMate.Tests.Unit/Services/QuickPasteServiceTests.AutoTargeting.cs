using Windows.Win32.Foundation;
using ClipMate.Core.Models.Configuration;
using ClipMate.Platform.Services;
using Moq;

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
        mockConfig.Setup(c => c.Configuration)
            .Returns(new ClipMateConfiguration
            {
                Preferences = new PreferencesConfiguration
                {
                    QuickPasteAutoTargetingEnabled = false,
                    QuickPasteFormattingStrings =
                    [
                        new() { Title = "Default", PasteKeystrokes = "^v", TitleTrigger = "*" },
                    ],
                    QuickPasteGoodTargets = ["NOTEPAD:EDIT"],
                    QuickPasteBadTargets = ["CLIPMATE:"],
                },
            });

        var mockWin32 = CreateMockWin32();
        mockWin32.Setup(p => p.GetForegroundWindow())
            .Returns(new HWND(12345));

        var service = new QuickPasteService(
            mockWin32.Object,
            CreateMockClipboardService().Object,
            mockConfig.Object,
            CreateMockMessenger().Object,
            CreateMockLogger().Object);

        // Act
        service.UpdateTarget();

        // Assert - GetForegroundWindow should NOT be called since auto-targeting is disabled
        mockWin32.Verify(p => p.GetForegroundWindow(), Times.Never);
        await Assert.That(service.GetCurrentTarget()).IsNull();
    }

    [Test]
    public void UpdateTarget_CallsGetForegroundWindow_WhenAutoTargetingEnabled()
    {
        // Arrange
        var mockConfig = CreateMockConfigurationService();
        mockConfig.Setup(p => p.Configuration)
            .Returns(new ClipMateConfiguration
            {
                Preferences = new PreferencesConfiguration
                {
                    QuickPasteAutoTargetingEnabled = true,
                    QuickPasteFormattingStrings =
                    [
                        new() { Title = "Default", PasteKeystrokes = "^v", TitleTrigger = "*" },
                    ],
                    QuickPasteGoodTargets = ["NOTEPAD:EDIT"],
                    QuickPasteBadTargets = ["CLIPMATE:"],
                },
            });

        var mockWin32 = CreateMockWin32();

        // Return null HWND (no window) to keep test simple
        mockWin32.Setup(p => p.GetForegroundWindow())
            .Returns(new HWND(nint.Zero));

        var service = new QuickPasteService(
            mockWin32.Object,
            CreateMockClipboardService().Object,
            mockConfig.Object,
            CreateMockMessenger().Object,
            CreateMockLogger().Object);

        // Act
        service.UpdateTarget();

        // Assert - GetForegroundWindow should be called when auto-targeting is enabled
        mockWin32.Verify(p => p.GetForegroundWindow(), Times.Once);
    }

    [Test]
    public void UpdateTarget_RespectsTargetLockEvenWhenAutoTargetingEnabled()
    {
        // Arrange
        var mockConfig = CreateMockConfigurationService();
        mockConfig.Setup(p => p.Configuration)
            .Returns(new ClipMateConfiguration
            {
                Preferences = new PreferencesConfiguration
                {
                    QuickPasteAutoTargetingEnabled = true,
                    QuickPasteFormattingStrings =
                    [
                        new() { Title = "Default", PasteKeystrokes = "^v", TitleTrigger = "*" },
                    ],
                    QuickPasteGoodTargets = ["NOTEPAD:EDIT"],
                    QuickPasteBadTargets = ["CLIPMATE:"],
                },
            });

        var mockWin32 = CreateMockWin32();
        var service = new QuickPasteService(
            mockWin32.Object,
            CreateMockClipboardService().Object,
            mockConfig.Object,
            CreateMockMessenger().Object,
            CreateMockLogger().Object);

        // Lock the target
        service.SetTargetLock(true);

        // Act
        service.UpdateTarget();

        // Assert - GetForegroundWindow should NOT be called since target is locked
        mockWin32.Verify(p => p.GetForegroundWindow(), Times.Never);
    }
}
