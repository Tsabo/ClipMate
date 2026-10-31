using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Reset to Defaults on the Hotkeys page.
/// </summary>
public class HotkeysOptionsDefaultsTests
{
    [Test]
    public async Task ResetToDefaults_ToggleAutoCapture_HasNoHotkey()
    {
        // Arrange
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());
        var hotkeyService = A.Fake<IHotkeyService>();
        var viewModel = new HotkeysOptionsViewModel(configurationService, hotkeyService,
            new HotkeyCoordinator(hotkeyService, configurationService, A.Fake<IMessenger>(), NullLogger<HotkeyCoordinator>.Instance))
        {
            ToggleAutoCapture = "Ctrl+Alt+A",
        };

        // Act
        viewModel.ResetToDefaultsCommand.Execute(null);

        // Assert: Ctrl+Alt+A is Append's shortcut in ClipMate's windows
        await Assert.That(viewModel.ToggleAutoCapture).IsEmpty();
        await Assert.That(viewModel.ShowWindow).IsEqualTo("Ctrl+Alt+C");
    }
}
