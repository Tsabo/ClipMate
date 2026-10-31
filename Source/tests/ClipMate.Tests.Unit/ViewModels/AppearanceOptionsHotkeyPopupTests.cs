using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The "Show clip when scrolling with hotkeys" option on the Appearance page.
/// </summary>
public class AppearanceOptionsHotkeyPopupTests
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
    public async Task LoadAndSave_ShowClipWhenScrollingWithHotkeys_RoundTripsTheSetting()
    {
        // Arrange
        _configuration.Appearance.ShowClipWhenScrollingWithHotkeys = false;

        // Act
        _viewModel.LoadAsync();
        var loaded = _viewModel.ShowClipWhenScrollingWithHotkeys;
        _viewModel.ShowClipWhenScrollingWithHotkeys = true;
        _viewModel.SaveAsync();

        // Assert
        await Assert.That(loaded).IsFalse();
        await Assert.That(_configuration.Appearance.ShowClipWhenScrollingWithHotkeys).IsTrue();
    }

    [Test]
    public async Task ResetToDefaults_ShowClipWhenScrollingWithHotkeys_IsOn()
    {
        // Arrange
        _viewModel.ShowClipWhenScrollingWithHotkeys = false;

        // Act
        _viewModel.ResetToDefaultsCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ShowClipWhenScrollingWithHotkeys).IsTrue();
    }
}
