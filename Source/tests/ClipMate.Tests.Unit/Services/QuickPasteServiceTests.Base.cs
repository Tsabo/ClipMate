using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base test fixture for QuickPasteService tests.
/// </summary>
public partial class QuickPasteServiceTests : TestFixtureBase
{
    private IClipboardService _mockClipboardService = null!;
    private IConfigurationService _mockConfigurationService = null!;
    private IDialogService _mockDialogService = null!;
    private ILogger<QuickPasteService> _mockLogger = null!;
    private IMacroExecutionService _mockMacroExecutionService = null!;
    private IMessenger _mockMessenger = null!;
    private ITemplateService _mockTemplateService = null!;
    private IWin32InputInterop _mockWin32 = null!;
    private ClipMateConfiguration _testConfiguration = null!;

    private QuickPasteService CreateService()
    {
        _mockWin32 = A.Fake<IWin32InputInterop>();
        _mockClipboardService = A.Fake<IClipboardService>();
        _mockConfigurationService = A.Fake<IConfigurationService>();
        _mockMessenger = A.Fake<IMessenger>();
        _mockMacroExecutionService = A.Fake<IMacroExecutionService>();
        _mockTemplateService = A.Fake<ITemplateService>();
        _mockDialogService = A.Fake<IDialogService>();
        _mockLogger = A.Fake<ILogger<QuickPasteService>>();

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

        A.CallTo(() => _mockConfigurationService.Configuration).Returns(_testConfiguration);

        return new QuickPasteService(
            _mockWin32,
            CreateMockPasteTarget(),
            _mockClipboardService,
            _mockConfigurationService,
            _mockMessenger,
            _mockMacroExecutionService,
            _mockTemplateService,
            _mockDialogService,
            _mockLogger);
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

    private IWin32InputInterop CreateMockWin32() => A.Fake<IWin32InputInterop>();

    private IPasteTarget CreateMockPasteTarget()
    {
        // No Paste Target unless a test sets one; FakeItEasy would otherwise return a dummy window
        var pasteTarget = A.Fake<IPasteTarget>();
        A.CallTo(() => pasteTarget.Window).Returns(null);
        return pasteTarget;
    }

    private IClipboardService CreateMockClipboardService() => A.Fake<IClipboardService>();

    private IMacroExecutionService CreateMockMacroExecutionService() => A.Fake<IMacroExecutionService>();

    private ITemplateService CreateMockTemplateService() => A.Fake<ITemplateService>();

    private IDialogService CreateMockDialogService() => A.Fake<IDialogService>();

    private IConfigurationService CreateMockConfigurationService()
    {
        var mock = A.Fake<IConfigurationService>();
        A.CallTo(() => mock.Configuration).Returns(_testConfiguration);
        return mock;
    }

    private IMessenger CreateMockMessenger() => A.Fake<IMessenger>();

    private ILogger<QuickPasteService> CreateMockLogger() => A.Fake<ILogger<QuickPasteService>>();
}
