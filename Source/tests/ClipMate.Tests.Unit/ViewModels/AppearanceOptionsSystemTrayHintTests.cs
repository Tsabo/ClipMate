using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The System Tray Hint option on the Appearance page.
/// </summary>
public class AppearanceOptionsSystemTrayHintTests
{
    private ClipMateConfiguration _configuration = null!;
    private AppearanceOptionsViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _configuration = new ClipMateConfiguration();
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);
        _viewModel = new AppearanceOptionsViewModel(configurationService, NullLogger<AppearanceOptionsViewModel>.Instance);
    }

    [Test]
    public async Task LoadAndSave_SystemTrayHint_RoundTripsTheSetting()
    {
        // Arrange
        _configuration.Appearance.SystemTrayHint = SystemTrayHint.Collection;

        // Act
        _viewModel.LoadAsync();
        var loaded = _viewModel.SystemTrayHint;
        _viewModel.SystemTrayHint = SystemTrayHint.ClipTitle;
        _viewModel.SaveAsync();

        // Assert
        await Assert.That(loaded).IsEqualTo(SystemTrayHint.Collection);
        await Assert.That(_configuration.Appearance.SystemTrayHint).IsEqualTo(SystemTrayHint.ClipTitle);
    }

    [Test]
    public async Task ResetToDefaults_SystemTrayHint_IsClipTitle()
    {
        // Arrange
        _viewModel.SystemTrayHint = SystemTrayHint.Collection;

        // Act
        _viewModel.ResetToDefaultsCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.SystemTrayHint).IsEqualTo(SystemTrayHint.ClipTitle);
    }
}
