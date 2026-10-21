using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using ClipMate.Platform;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;
using ConfigModels = ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.ViewModels;

[Category("OptionsViewModel")]
[Category("ViewModel")]
public class OptionsViewModelTests
{
    private AdvancedOptionsViewModel _advancedViewModel = null!;
    private AppearanceOptionsViewModel _appearanceViewModel = null!;
    private ApplicationProfilesOptionsViewModel _applicationProfilesViewModel = null!;
    private CapturingOptionsViewModel _capturingViewModel = null!;
    private DatabaseOptionsViewModel _databaseViewModel = null!;
    private EditorOptionsViewModel _editorViewModel = null!;

    private EncryptionOptionsViewModel _encryptionViewModel = null!;

    // Child ViewModels
    private GeneralOptionsViewModel _generalViewModel = null!;
    private HotkeysOptionsViewModel _hotkeysViewModel = null!;
    private IConfigurationService _mockConfigurationService = null!;
    private IHotkeyService _mockHotkeyService = null!;
    private ILogger<OptionsViewModel> _mockLogger = null!;
    private IMessenger _mockMessenger = null!;
    private IApplicationProfileService _mockProfileService = null!;
    private ISoundService _mockSoundService = null!;
    private IStartupManager _mockStartupManager = null!;
    private PowerPasteOptionsViewModel _powerPasteViewModel = null!;
    private PrintOptionsViewModel _printOptionsViewModel = null!;
    private DevicesOptionsViewModel _devicesViewModel = null!;
    private QuickPasteOptionsViewModel _quickPasteViewModel = null!;
    private SoundsOptionsViewModel _soundsViewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockConfigurationService = A.Fake<IConfigurationService>();
        _mockStartupManager = A.Fake<IStartupManager>();
        _mockMessenger = A.Fake<IMessenger>();
        _mockLogger = A.Fake<ILogger<OptionsViewModel>>();
        _mockProfileService = A.Fake<IApplicationProfileService>();
        _mockSoundService = A.Fake<ISoundService>();
        A.CallTo(() => _mockSoundService.PlaySoundAsync(A<SoundEvent>.Ignored, CancellationToken.None)).Returns(Task.CompletedTask);
        _mockHotkeyService = A.Fake<IHotkeyService>();

        // Setup default configuration
        var config = new ConfigModels.ClipMateConfiguration
        {
            Preferences = new ConfigModels.PreferencesConfiguration(),
            MonacoEditor = new ConfigModels.MonacoEditorConfiguration(),
            Appearance = new ConfigModels.AppearanceConfiguration(),
        };

        A.CallTo(() => _mockConfigurationService.Configuration).Returns(config);

        // Create all child ViewModels
        _generalViewModel = new GeneralOptionsViewModel(
            _mockConfigurationService,
            _mockStartupManager,
            _mockMessenger,
            A.Fake<ILogger<GeneralOptionsViewModel>>());

        _appearanceViewModel = new AppearanceOptionsViewModel(
            _mockConfigurationService,
            A.Fake<ILogger<AppearanceOptionsViewModel>>());

        _powerPasteViewModel = new PowerPasteOptionsViewModel(
            _mockConfigurationService,
            A.Fake<ILogger<PowerPasteOptionsViewModel>>());

        _quickPasteViewModel = new QuickPasteOptionsViewModel(
            _mockConfigurationService,
            _mockMessenger,
            A.Fake<ILogger<QuickPasteOptionsViewModel>>());

        _editorViewModel = new EditorOptionsViewModel(
            _mockConfigurationService,
            A.Fake<ILogger<EditorOptionsViewModel>>());

        _capturingViewModel = new CapturingOptionsViewModel(
            _mockConfigurationService,
            A.Fake<ILogger<CapturingOptionsViewModel>>());

        _applicationProfilesViewModel = new ApplicationProfilesOptionsViewModel(
            A.Fake<ILogger<ApplicationProfilesOptionsViewModel>>(),
            _mockProfileService);

        _soundsViewModel = new SoundsOptionsViewModel(
            _mockConfigurationService,
            _mockSoundService,
            A.Fake<ILogger<SoundsOptionsViewModel>>());

        var mockHotkeyCoordinator = A.Fake<HotkeyCoordinator>(o => o.WithArgumentsForConstructor(() => new HotkeyCoordinator(
            _mockHotkeyService,
            _mockConfigurationService,
            _mockMessenger,
            A.Fake<ILogger<HotkeyCoordinator>>())));

        _hotkeysViewModel = new HotkeysOptionsViewModel(
            _mockConfigurationService,
            _mockHotkeyService,
            mockHotkeyCoordinator);

        _databaseViewModel = new DatabaseOptionsViewModel(
            _mockConfigurationService,
            A.Fake<IClipMate7ImportService>(),
            A.Fake<IDatabaseManager>(),
            A.Fake<IMessenger>(),
            A.Fake<ILogger<DatabaseOptionsViewModel>>(),
            A.Fake<IDatabaseIdService>());

        _advancedViewModel = new AdvancedOptionsViewModel(
            _mockConfigurationService,
            A.Fake<IDialogService>(),
            A.Fake<ILogger<AdvancedOptionsViewModel>>());

        _encryptionViewModel = new EncryptionOptionsViewModel();

        _printOptionsViewModel = new PrintOptionsViewModel(
            _mockConfigurationService);

        _devicesViewModel = new DevicesOptionsViewModel(
            _mockConfigurationService,
            A.Fake<IDeviceIdentityService>(),
            A.Fake<IDeviceDiscoveryService>(),
            A.Fake<ILogger<DevicesOptionsViewModel>>());
    }

    private OptionsViewModel CreateViewModel() =>
        new(
            _mockConfigurationService,
            _mockMessenger,
            _mockLogger,
            _generalViewModel,
            _appearanceViewModel,
            _powerPasteViewModel,
            _quickPasteViewModel,
            _editorViewModel,
            _capturingViewModel,
            _applicationProfilesViewModel,
            _soundsViewModel,
            _hotkeysViewModel,
            _databaseViewModel,
            _advancedViewModel,
            _encryptionViewModel,
            _printOptionsViewModel,
            _devicesViewModel);

    [Test]
    public async Task Constructor_WithoutProfileService_ShouldInitializeWithoutProfiles()
    {
        // Arrange - Create ViewModel without profile service
        var applicationProfilesViewModel = new ApplicationProfilesOptionsViewModel(
            A.Fake<ILogger<ApplicationProfilesOptionsViewModel>>()); // No profile service

        // Act
        var viewModel = new OptionsViewModel(
            _mockConfigurationService,
            _mockMessenger,
            _mockLogger,
            _generalViewModel,
            _appearanceViewModel,
            _powerPasteViewModel,
            _quickPasteViewModel,
            _editorViewModel,
            _capturingViewModel,
            applicationProfilesViewModel,
            _soundsViewModel,
            _hotkeysViewModel,
            _databaseViewModel,
            _advancedViewModel,
            _encryptionViewModel,
            _printOptionsViewModel,
            _devicesViewModel);

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
        A.CallTo(() => _mockProfileService.IsApplicationProfilesEnabled()).Returns(true);
        A.CallTo(() => _mockProfileService.GetAllProfilesAsync(CancellationToken.None))
            .Returns(new Dictionary<string, ApplicationProfile>());

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadConfigurationAsync();

        // Assert
        await Assert.That(viewModel.ApplicationProfiles.EnableApplicationProfiles).IsTrue();
        A.CallTo(() => _mockProfileService.IsApplicationProfilesEnabled()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mockProfileService.GetAllProfilesAsync(CancellationToken.None)).MustHaveHappenedOnceExactly();
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

        A.CallTo(() => _mockProfileService.GetAllProfilesAsync(CancellationToken.None))
            .Returns(profiles);

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
        var mockLogger = A.Fake<ILogger<ApplicationProfilesOptionsViewModel>>();
        var applicationProfilesViewModel = new ApplicationProfilesOptionsViewModel(mockLogger);

        // Act
        await applicationProfilesViewModel.LoadApplicationProfilesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_applicationProfilesViewModel.ApplicationProfileNodes).IsEmpty();
        A.CallTo(mockLogger).Where(call => call.Method.Name == "Log"
                && call.GetArgument<LogLevel>(0) == LogLevel.Warning
                && call.GetArgument<object>(2)!.ToString()!.Contains("not available"))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task DeleteAllProfilesAsync_ShouldClearProfilesAndCallService()
    {
        // Arrange
        var profiles = new Dictionary<string, ApplicationProfile>
        {
            ["NOTEPAD"] = ApplicationProfileTestFixtures.GetNotepadProfile(),
        };

        A.CallTo(() => _mockProfileService.GetAllProfilesAsync(CancellationToken.None))
            .Returns(profiles);

        var viewModel = CreateViewModel();

        await viewModel.ApplicationProfiles.LoadApplicationProfilesCommand.ExecuteAsync(null);
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes).Count().IsEqualTo(1);

        // Act
        await viewModel.ApplicationProfiles.DeleteAllProfilesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.ApplicationProfiles.ApplicationProfileNodes).IsEmpty();
        A.CallTo(() => _mockProfileService.DeleteAllProfilesAsync(CancellationToken.None)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task DeleteAllProfilesAsync_WithoutProfileService_ShouldLogWarning()
    {
        // Arrange - Create ViewModel without profile service
        var mockLogger = A.Fake<ILogger<ApplicationProfilesOptionsViewModel>>();
        var applicationProfilesViewModel = new ApplicationProfilesOptionsViewModel(mockLogger);

        // Act
        await applicationProfilesViewModel.DeleteAllProfilesCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(mockLogger).Where(call => call.Method.Name == "Log"
                && call.GetArgument<LogLevel>(0) == LogLevel.Warning
                && call.GetArgument<object>(2)!.ToString()!.Contains("not available"))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task OkCommand_ShouldSaveProfileEnabledState()
    {
        // Arrange
        A.CallTo(() => _mockStartupManager.IsEnabledAsync()).Returns((true, false, string.Empty));

        var viewModel = CreateViewModel();

        viewModel.ApplicationProfiles.EnableApplicationProfiles = true;

        // Act
        await viewModel.OkCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _mockProfileService.SetApplicationProfilesEnabled(true)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mockConfigurationService.SaveAsync(A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task OkCommand_ShouldUpdateAllProfileNodes()
    {
        // Arrange
        A.CallTo(() => _mockStartupManager.IsEnabledAsync()).Returns((true, false, string.Empty));

        var notepadProfile = ApplicationProfileTestFixtures.GetNotepadProfile();
        var chromeProfile = ApplicationProfileTestFixtures.GetChromeProfile();

        var profiles = new Dictionary<string, ApplicationProfile>
        {
            ["NOTEPAD"] = notepadProfile,
            ["CHROME"] = chromeProfile,
        };

        A.CallTo(() => _mockProfileService.GetAllProfilesAsync(CancellationToken.None))
            .Returns(profiles);

        var viewModel = CreateViewModel();

        await viewModel.ApplicationProfiles.LoadApplicationProfilesCommand.ExecuteAsync(null);

        // Modify enabled states
        viewModel.ApplicationProfiles.ApplicationProfileNodes[0].Enabled = false;
        viewModel.ApplicationProfiles.ApplicationProfileNodes[1].Enabled = true;

        // Act
        await viewModel.OkCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _mockProfileService.UpdateProfileAsync(A<ApplicationProfile>.Ignored, CancellationToken.None)).MustHaveHappened(2, Times.Exactly);
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
