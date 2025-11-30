using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using ConfigModels = ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.ViewModels;

[Category("OptionsViewModel")]
[Category("ViewModel")]
public class OptionsViewModelTests
{
    private Mock<IConfigurationService> _mockConfigurationService = null!;
    private Mock<ILogger<OptionsViewModel>> _mockLogger = null!;
    private Mock<IMessenger> _mockMessenger = null!;
    private Mock<IApplicationProfileService> _mockProfileService = null!;
    private Mock<IStartupManager> _mockStartupManager = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockConfigurationService = new Mock<IConfigurationService>();
        _mockStartupManager = new Mock<IStartupManager>();
        _mockMessenger = new Mock<IMessenger>();
        _mockLogger = new Mock<ILogger<OptionsViewModel>>();
        _mockProfileService = new Mock<IApplicationProfileService>();

        // Setup default configuration
        var config = new ConfigModels.ClipMateConfiguration
        {
            Preferences = new ConfigModels.PreferencesConfiguration(),
            MonacoEditor = new ConfigModels.MonacoEditorConfiguration(),
        };

        _mockConfigurationService.Setup(x => x.Configuration).Returns(config);
    }

    [Test]
    public async Task Constructor_WithoutProfileService_ShouldInitializeWithoutProfiles()
    {
        // Act
        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object);

        // Assert
        await Assert.That(viewModel).IsNotNull();
        await Assert.That(viewModel.ApplicationProfileNodes).IsEmpty();
        await Assert.That(viewModel.EnableApplicationProfiles).IsFalse();
    }

    [Test]
    public async Task Constructor_WithProfileService_ShouldInitializeWithProfiles()
    {
        // Act
        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            _mockProfileService.Object);

        // Assert
        await Assert.That(viewModel).IsNotNull();
        await Assert.That(viewModel.ApplicationProfileNodes).IsNotNull();
    }

    [Test]
    public async Task LoadConfigurationAsync_WithProfileService_ShouldLoadProfilesEnabledState()
    {
        // Arrange
        _mockProfileService.Setup(p => p.IsApplicationProfilesEnabled()).Returns(true);
        _mockProfileService.Setup(p => p.GetAllProfilesAsync(default))
            .ReturnsAsync(new Dictionary<string, ApplicationProfile>());

        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            _mockProfileService.Object);

        // Act
        await viewModel.LoadConfigurationAsync();

        // Assert
        await Assert.That(viewModel.EnableApplicationProfiles).IsTrue();
        _mockProfileService.Verify(p => p.IsApplicationProfilesEnabled(), Times.Once);
        _mockProfileService.Verify(p => p.GetAllProfilesAsync(default), Times.Once);
    }

    [Test]
    public async Task LoadApplicationProfilesAsync_ShouldLoadAndOrderProfiles()
    {
        // Arrange
        var profiles = new Dictionary<string, ApplicationProfile>
        {
            ["NOTEPAD"] = ApplicationProfileTestFixtures.GetNotepadProfile(),
            ["CHROME"] = ApplicationProfileTestFixtures.GetChromeProfile(),
            ["DEVENV"] = ApplicationProfileTestFixtures.GetDevenvProfile(),
        };

        _mockProfileService.Setup(p => p.GetAllProfilesAsync(default))
            .ReturnsAsync(profiles);

        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            _mockProfileService.Object);

        // Act
        await viewModel.LoadApplicationProfilesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.ApplicationProfileNodes).HasCount().EqualTo(3);
        await Assert.That(viewModel.ApplicationProfileNodes[0].Profile.ApplicationName).IsEqualTo("CHROME");
        await Assert.That(viewModel.ApplicationProfileNodes[1].Profile.ApplicationName).IsEqualTo("DEVENV");
        await Assert.That(viewModel.ApplicationProfileNodes[2].Profile.ApplicationName).IsEqualTo("NOTEPAD");
    }

    [Test]
    public async Task LoadApplicationProfilesAsync_WithoutProfileService_ShouldLogWarning()
    {
        // Arrange
        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            null);

        // Act
        await viewModel.LoadApplicationProfilesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.ApplicationProfileNodes).IsEmpty();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task DeleteAllProfilesAsync_ShouldClearProfilesAndCallService()
    {
        // Arrange
        var profiles = new Dictionary<string, ApplicationProfile>
        {
            ["NOTEPAD"] = ApplicationProfileTestFixtures.GetNotepadProfile(),
        };

        _mockProfileService.Setup(p => p.GetAllProfilesAsync(default))
            .ReturnsAsync(profiles);

        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            _mockProfileService.Object);

        await viewModel.LoadApplicationProfilesCommand.ExecuteAsync(null);
        await Assert.That(viewModel.ApplicationProfileNodes).HasCount().EqualTo(1);

        // Act
        await viewModel.DeleteAllProfilesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.ApplicationProfileNodes).IsEmpty();
        _mockProfileService.Verify(p => p.DeleteAllProfilesAsync(default), Times.Once);
    }

    [Test]
    public async Task DeleteAllProfilesAsync_WithoutProfileService_ShouldLogWarning()
    {
        // Arrange
        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            null);

        // Act
        await viewModel.DeleteAllProfilesCommand.ExecuteAsync(null);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task OkCommand_ShouldSaveProfileEnabledState()
    {
        // Arrange
        _mockStartupManager.Setup(x => x.IsEnabledAsync()).ReturnsAsync((true, false, string.Empty));

        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            _mockProfileService.Object);

        viewModel.EnableApplicationProfiles = true;

        // Act
        await viewModel.OkCommand.ExecuteAsync(null);

        // Assert
        _mockProfileService.Verify(p => p.SetApplicationProfilesEnabled(true), Times.Once);
        _mockConfigurationService.Verify(x => x.SaveAsync(), Times.Once);
    }

    [Test]
    public async Task OkCommand_ShouldUpdateAllProfileNodes()
    {
        // Arrange
        _mockStartupManager.Setup(x => x.IsEnabledAsync()).ReturnsAsync((true, false, string.Empty));

        var notepadProfile = ApplicationProfileTestFixtures.GetNotepadProfile();
        var chromeProfile = ApplicationProfileTestFixtures.GetChromeProfile();

        var profiles = new Dictionary<string, ApplicationProfile>
        {
            ["NOTEPAD"] = notepadProfile,
            ["CHROME"] = chromeProfile,
        };

        _mockProfileService.Setup(p => p.GetAllProfilesAsync(default))
            .ReturnsAsync(profiles);

        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            _mockProfileService.Object);

        await viewModel.LoadApplicationProfilesCommand.ExecuteAsync(null);

        // Modify enabled states
        viewModel.ApplicationProfileNodes[0].Enabled = false;
        viewModel.ApplicationProfileNodes[1].Enabled = true;

        // Act
        await viewModel.OkCommand.ExecuteAsync(null);

        // Assert
        _mockProfileService.Verify(
            p => p.UpdateProfileAsync(It.IsAny<ApplicationProfile>(), default),
            Times.Exactly(2));
    }

    [Test]
    public async Task EnableApplicationProfiles_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            _mockProfileService.Object);

        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.EnableApplicationProfiles))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.EnableApplicationProfiles = true;

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(viewModel.EnableApplicationProfiles).IsTrue();
    }
}
