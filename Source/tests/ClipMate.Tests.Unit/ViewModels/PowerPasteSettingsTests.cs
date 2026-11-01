using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Power Paste's Explode and Loop are single saved settings shared by the menu, the toolbar and the Options dialog;
/// Shield is no longer offered but its setting is kept.
/// </summary>
public class PowerPasteSettingsTests : TestFixtureBase
{
    private readonly ClipMateConfiguration _configuration = new();
    private readonly IConfigurationService _configurationService = A.Fake<IConfigurationService>();

    public PowerPasteSettingsTests() => A.CallTo(() => _configurationService.Configuration).Returns(_configuration);

    [Test]
    public async Task IsExplodeMode_ShowsTheSavedSetting()
    {
        // Arrange
        _configuration.Preferences.PowerPasteExplode = true;
        using var mainMenu = CreateMainMenu();

        // Act & Assert
        await Assert.That(mainMenu.IsExplodeMode).IsTrue();
    }

    [Test]
    public async Task IsExplodeMode_WhenToggled_SavesTheSetting()
    {
        // Arrange
        using var mainMenu = CreateMainMenu();

        // Act
        mainMenu.IsExplodeMode = true;

        // Assert
        await Assert.That(_configuration.Preferences.PowerPasteExplode).IsTrue();
        A.CallTo(() => _configurationService.SaveAsync(A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task IsLoopMode_WhenToggled_SavesTheSetting()
    {
        // Arrange
        using var mainMenu = CreateMainMenu();

        // Act
        mainMenu.IsLoopMode = true;

        // Assert
        await Assert.That(_configuration.Preferences.PowerPasteLoop).IsTrue();
        A.CallTo(() => _configurationService.SaveAsync(A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ConfigurationSaved_FromTheOptionsDialog_RefreshesTheMenuAndToolbar()
    {
        // Arrange
        using var mainMenu = CreateMainMenu();
        var changed = new List<string?>();
        mainMenu.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _configuration.Preferences.PowerPasteExplode = true;

        // Act
        _configurationService.Saved += Raise.WithEmpty();

        // Assert
        await Assert.That(changed).Contains(nameof(MainMenuViewModel.IsExplodeMode));
        await Assert.That(mainMenu.IsExplodeMode).IsTrue();
    }

    [Test]
    public async Task PowerPasteOptions_Save_KeepsTheShieldSetting()
    {
        // Arrange - Shield is no longer on the dialog; an imported value must survive saving it
        _configuration.Preferences.PowerPasteShield = false;
        var options = new PowerPasteOptionsViewModel(_configurationService,
            new OutboundFilterOptionsViewModel(_configurationService, A.Fake<ICurrentClipWriter>()), A.Fake<ILogger<PowerPasteOptionsViewModel>>());
        options.LoadAsync();

        // Act
        options.ResetToDefaultsCommand.Execute(null);
        options.SaveAsync();

        // Assert
        await Assert.That(_configuration.Preferences.PowerPasteShield).IsFalse();
    }

    private MainMenuViewModel CreateMainMenu()
    {
        var serviceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => serviceProvider.GetService(typeof(IConfigurationService))).Returns(_configurationService);

        return new MainMenuViewModel(
            A.Fake<IMessenger>(),
            A.Fake<IUndoService>(),
            A.Fake<IClipViewerWindowManager>(),
            A.Fake<IClipboardService>(),
            A.Fake<IAutoAppendService>(),
            A.Fake<IPowerPasteService>(),
            A.Fake<IQuickPasteService>(),
            A.Fake<IPasteTarget>(),
            A.Fake<IPasteTargetTracker>(),
            serviceProvider);
    }
}
