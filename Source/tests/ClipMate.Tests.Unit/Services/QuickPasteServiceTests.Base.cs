using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base test fixture for QuickPasteService tests.
/// </summary>
public partial class QuickPasteServiceTests : TestFixtureBase
{
    private Mock<IClipboardService> _mockClipboardService = null!;
    private Mock<IConfigurationService> _mockConfigurationService = null!;
    private Mock<IDialogService> _mockDialogService = null!;
    private Mock<ILogger<QuickPasteService>> _mockLogger = null!;
    private Mock<IMacroExecutionService> _mockMacroExecutionService = null!;
    private Mock<IMessenger> _mockMessenger = null!;
    private Mock<ITemplateService> _mockTemplateService = null!;
    private Mock<IWin32InputInterop> _mockWin32 = null!;
    private ClipMateConfiguration _testConfiguration = null!;

    private QuickPasteService CreateService()
    {
        _mockWin32 = new Mock<IWin32InputInterop>();
        _mockClipboardService = new Mock<IClipboardService>();
        _mockConfigurationService = new Mock<IConfigurationService>();
        _mockMessenger = new Mock<IMessenger>();
        _mockMacroExecutionService = new Mock<IMacroExecutionService>();
        _mockTemplateService = new Mock<ITemplateService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockLogger = new Mock<ILogger<QuickPasteService>>();

        // Setup default test configuration
        _testConfiguration = new ClipMateConfiguration
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
                        Title = "Word with Date",
                        Preamble = "#CURRENTDATE# - ",
                        PasteKeystrokes = "^v",
                        Postamble = "",
                        TitleTrigger = "Microsoft Word",
                    },
                ],
                QuickPasteGoodTargets =
                [
                    "NOTEPAD:EDIT",
                    "NOTEPAD++:",
                    "DEVENV:",
                    "CODE:",
                ],
                QuickPasteBadTargets =
                [
                    "CLIPMATE:",
                    "EXPLORER:SHELLTABWINDOWCLASS",
                ],
            },
        };

        _mockConfigurationService.Setup(x => x.Configuration).Returns(_testConfiguration);

        return new QuickPasteService(
            _mockWin32.Object,
            _mockClipboardService.Object,
            _mockConfigurationService.Object,
            _mockMessenger.Object,
            _mockMacroExecutionService.Object,
            _mockTemplateService.Object,
            _mockDialogService.Object,
            _mockLogger.Object);
    }

    private Clip CreateTestClip() =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test clipboard content",
            CapturedAt = DateTimeOffset.Now,
            SourceApplicationName = "TestApp.exe",
            ContentHash = "TestHash",
        };

    private Mock<IWin32InputInterop> CreateMockWin32() => new();

    private Mock<IClipboardService> CreateMockClipboardService() => new();

    private Mock<IMacroExecutionService> CreateMockMacroExecutionService() => new();

    private Mock<ITemplateService> CreateMockTemplateService() => new();

    private Mock<IDialogService> CreateMockDialogService() => new();

    private Mock<IConfigurationService> CreateMockConfigurationService()
    {
        var mock = new Mock<IConfigurationService>();
        mock.Setup(p => p.Configuration).Returns(_testConfiguration);
        return mock;
    }

    private Mock<IMessenger> CreateMockMessenger() => new();

    private Mock<ILogger<QuickPasteService>> CreateMockLogger() => new();
}
