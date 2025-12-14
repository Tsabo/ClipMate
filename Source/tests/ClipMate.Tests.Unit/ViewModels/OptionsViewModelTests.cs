using ClipMate.App.Services;
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
    private ApplicationProfilesOptionsViewModel _applicationProfilesViewModel = null!;
    private CapturingOptionsViewModel _capturingViewModel = null!;
    private DatabaseOptionsViewModel _databaseViewModel = null!;
    private EditorOptionsViewModel _editorViewModel = null!;

    // Child ViewModels
    private GeneralOptionsViewModel _generalViewModel = null!;
    private HotkeysOptionsViewModel _hotkeysViewModel = null!;
    private Mock<IConfigurationService> _mockConfigurationService = null!;
    private Mock<IHotkeyService> _mockHotkeyService = null!;
    private Mock<ILogger<OptionsViewModel>> _mockLogger = null!;
    private Mock<IMessenger> _mockMessenger = null!;
    private Mock<IApplicationProfileService> _mockProfileService = null!;
    private Mock<ISoundService> _mockSoundService = null!;
    private Mock<IStartupManager> _mockStartupManager = null!;
    private PowerPasteOptionsViewModel _powerPasteViewModel = null!;
    private QuickPasteOptionsViewModel _quickPasteViewModel = null!;
    private SoundsOptionsViewModel _soundsViewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockConfigurationService = new Mock<IConfigurationService>();
        _mockStartupManager = new Mock<IStartupManager>();
        _mockMessenger = new Mock<IMessenger>();
        _mockLogger = new Mock<ILogger<OptionsViewModel>>();
        _mockProfileService = new Mock<IApplicationProfileService>();
        _mockSoundService = new Mock<ISoundService>();
        _mockSoundService.Setup(p => p.PlaySoundAsync(It.IsAny<SoundEvent>(), default)).Returns(Task.CompletedTask);
        _mockHotkeyService = new Mock<IHotkeyService>();

        // Setup default configuration
        var config = new ConfigModels.ClipMateConfiguration
        {
            Preferences = new ConfigModels.PreferencesConfiguration(),
            MonacoEditor = new ConfigModels.MonacoEditorConfiguration(),
        };

        _mockConfigurationService.Setup(p => p.Configuration).Returns(config);

        // Create all child ViewModels
        _generalViewModel = new GeneralOptionsViewModel(
            _mockConfigurationService.Object,
            _mockStartupManager.Object,
            _mockMessenger.Object,
            new Mock<ILogger<GeneralOptionsViewModel>>().Object);

        _powerPasteViewModel = new PowerPasteOptionsViewModel(
            _mockConfigurationService.Object,
            new Mock<ILogger<PowerPasteOptionsViewModel>>().Object);

        _quickPasteViewModel = new QuickPasteOptionsViewModel(
            _mockConfigurationService.Object,
            _mockMessenger.Object,
            new Mock<ILogger<QuickPasteOptionsViewModel>>().Object);

        _editorViewModel = new EditorOptionsViewModel(
            _mockConfigurationService.Object,
            new Mock<ILogger<EditorOptionsViewModel>>().Object);

        _capturingViewModel = new CapturingOptionsViewModel(
            _mockConfigurationService.Object,
            new Mock<ILogger<CapturingOptionsViewModel>>().Object);

        _applicationProfilesViewModel = new ApplicationProfilesOptionsViewModel(
            new Mock<ILogger<ApplicationProfilesOptionsViewModel>>().Object,
            _mockProfileService.Object);

        _soundsViewModel = new SoundsOptionsViewModel(
            _mockConfigurationService.Object,
            _mockSoundService.Object,
            new Mock<ILogger<SoundsOptionsViewModel>>().Object);

        var mockHotkeyCoordinator = new Mock<HotkeyCoordinator>(
            _mockHotkeyService.Object,
            _mockConfigurationService.Object,
            _mockMessenger.Object,
            new Mock<ILogger<HotkeyCoordinator>>().Object);

        _hotkeysViewModel = new HotkeysOptionsViewModel(
            _mockConfigurationService.Object,
            _mockHotkeyService.Object,
            mockHotkeyCoordinator.Object);

        _databaseViewModel = new DatabaseOptionsViewModel(
            _mockConfigurationService.Object,
            new Mock<ILogger<DatabaseOptionsViewModel>>().Object);
    }

    private OptionsViewModel CreateViewModel() =>
        new(
            _mockConfigurationService.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            _generalViewModel,
            _powerPasteViewModel,
            _quickPasteViewModel,
            _editorViewModel,
            _capturingViewModel,
            _applicationProfilesViewModel,
            _soundsViewModel,
            _hotkeysViewModel,
            _databaseViewModel);

    [Test]
    public async Task Constructor_WithoutProfileService_ShouldInitializeWithoutProfiles()
    {
        // Arrange - Create ViewModel without profile service
        var applicationProfilesViewModel = new ApplicationProfilesOptionsViewModel(
            new Mock<ILogger<ApplicationProfilesOptionsViewModel>>().Object); // No profile service

        // Act
        var viewModel = new OptionsViewModel(
            _mockConfigurationService.Object,
            _mockMessenger.Object,
            _mockLogger.Object,
            _generalViewModel,
            _powerPasteViewModel,
            _quickPasteViewModel,
            _editorViewModel,
            _capturingViewModel,
            applicationProfilesViewModel,
            _soundsViewModel,
            _hotkeysViewModel,
            _databaseViewModel);

        // Assert
        await Assert.That(viewModel).IsNotNull();
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes).IsEmpty();
        await Assert.That(viewModel.ApplicationProfiles.EnableApplicationProfiles).IsFalse();
    }

    [Test]
    public async Task Constructor_WithProfileService_ShouldInitializeWithProfiles()
    {
        // Act
        var viewModel = CreateViewModel();

        // Assert
        await Assert.That(viewModel).IsNotNull();
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes).IsNotNull();
    }

    [Test]
    public async Task LoadConfigurationAsync_WithProfileService_ShouldLoadProfilesEnabledState()
    {
        // Arrange
        _mockProfileService.Setup(p => p.IsApplicationProfilesEnabled()).Returns(true);
        _mockProfileService.Setup(p => p.GetAllProfilesAsync(CancellationToken.None))
            .ReturnsAsync(new Dictionary<string, ApplicationProfile>());

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadConfigurationAsync();

        // Assert
        await Assert.That(viewModel.ApplicationProfiles.EnableApplicationProfiles).IsTrue();
        _mockProfileService.Verify(p => p.IsApplicationProfilesEnabled(), Times.Once);
        _mockProfileService.Verify(p => p.GetAllProfilesAsync(CancellationToken.None), Times.Once);
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

        _mockProfileService.Setup(p => p.GetAllProfilesAsync(CancellationToken.None))
            .ReturnsAsync(profiles);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.ApplicationProfiles.LoadApplicationProfilesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes).Count().IsEqualTo(3);
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes[0].Profile.ApplicationName).IsEqualTo("CHROME");
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes[1].Profile.ApplicationName).IsEqualTo("DEVENV");
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes[2].Profile.ApplicationName).IsEqualTo("NOTEPAD");
    }

    [Test]
    public async Task LoadApplicationProfilesAsync_WithoutProfileService_ShouldLogWarning()
    {
        // Arrange - Create ViewModel without profile service
        var mockLogger = new Mock<ILogger<ApplicationProfilesOptionsViewModel>>();
        var applicationProfilesViewModel = new ApplicationProfilesOptionsViewModel(mockLogger.Object);

        // Act
        await applicationProfilesViewModel.LoadApplicationProfilesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_applicationProfilesViewModel.ApplicationProfileNodes).IsEmpty();
        mockLogger.Verify(
            p => p.Log(
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

        _mockProfileService.Setup(p => p.GetAllProfilesAsync(CancellationToken.None))
            .ReturnsAsync(profiles);

        var viewModel = CreateViewModel();

        await viewModel.ApplicationProfiles.LoadApplicationProfilesCommand.ExecuteAsync(null);
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes).Count().IsEqualTo(1);

        // Act
        await viewModel.ApplicationProfiles.DeleteAllProfilesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes).IsEmpty();
        _mockProfileService.Verify(p => p.DeleteAllProfilesAsync(CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task DeleteAllProfilesAsync_WithoutProfileService_ShouldLogWarning()
    {
        // Arrange - Create ViewModel without profile service
        var mockLogger = new Mock<ILogger<ApplicationProfilesOptionsViewModel>>();
        var applicationProfilesViewModel = new ApplicationProfilesOptionsViewModel(mockLogger.Object);

        // Act
        await applicationProfilesViewModel.DeleteAllProfilesCommand.ExecuteAsync(null);

        // Assert
        mockLogger.Verify(
            p => p.Log(
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
        _mockStartupManager.Setup(p => p.IsEnabledAsync()).ReturnsAsync((true, false, string.Empty));

        var viewModel = CreateViewModel();

        viewModel.ApplicationProfiles.EnableApplicationProfiles = true;

        // Act
        await viewModel.OkCommand.ExecuteAsync(null);

        // Assert
        _mockProfileService.Verify(p => p.SetApplicationProfilesEnabled(true), Times.Once);
        _mockConfigurationService.Verify(p => p.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
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

        _mockProfileService.Setup(p => p.GetAllProfilesAsync(CancellationToken.None))
            .ReturnsAsync(profiles);

        var viewModel = CreateViewModel();

        await viewModel.ApplicationProfiles.LoadApplicationProfilesCommand.ExecuteAsync(null);

        // Modify enabled states
        viewModel.ApplicationProfiles.ApplicationProfileNodes[0].Enabled = false;
        viewModel.ApplicationProfiles.ApplicationProfileNodes[1].Enabled = true;

        // Act
        await viewModel.OkCommand.ExecuteAsync(null);

        // Assert
        _mockProfileService.Verify(
            p => p.UpdateProfileAsync(It.IsAny<ApplicationProfile>(), CancellationToken.None),
            Times.Exactly(2));
    }

    [Test]
    public async Task EnableApplicationProfiles_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _applicationProfilesViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(_applicationProfilesViewModel.EnableApplicationProfiles))
                propertyChangedRaised = true;
        };

        // Act
        _applicationProfilesViewModel.EnableApplicationProfiles = true;

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(_applicationProfilesViewModel.EnableApplicationProfiles).IsTrue();
    }
}
