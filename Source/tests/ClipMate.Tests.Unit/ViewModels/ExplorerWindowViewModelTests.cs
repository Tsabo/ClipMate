using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for ExplorerWindowViewModel.
/// Following TDD: Tests written FIRST, then implementation.
/// </summary>
public class ExplorerWindowViewModelTests
{
    private ExplorerWindowViewModel CreateViewModel()
    {
        var mockMessenger = A.Fake<IMessenger>();
        var mockClipService = A.Fake<IClipService>();
        var mockFolderService = A.Fake<IFolderService>();
        var mockCollectionService = A.Fake<ICollectionService>();
        var mockConfigurationService = A.Fake<IConfigurationService>();
        var mockSearchService = A.Fake<ISearchService>();
        var mockContextFactory = A.Fake<IDatabaseContextFactory>();
        var mockQuickPasteService = A.Fake<IQuickPasteService>();
        var mockLogger = A.Fake<ILogger<ClipListViewModel>>();
        var mockTreeLogger = A.Fake<ILogger<CollectionTreeViewModel>>();
        var mockMainLogger = A.Fake<ILogger<ExplorerWindowViewModel>>();
        var mockCollectionTreeBuilder = A.Fake<ICollectionTreeBuilder>();

        var collectionTreeVm = new CollectionTreeViewModel(
            mockCollectionService,
            mockFolderService,
            mockClipService,
            mockConfigurationService,
            mockMessenger,
            mockCollectionTreeBuilder,
            mockTreeLogger,
            new SearchResultsCache(),
            A.Fake<IDatabaseIdService>(),
            A.Fake<IRemoteDatabaseService>(),
            A.Fake<IRemoteDatabaseTreeBuilder>(),
            A.Fake<ISharedDatabaseService>(),
            A.Fake<ClipMate.App.Services.IPeerDialogService>());

        var clipListVm = new ClipListViewModel(
            mockCollectionService,
            mockFolderService,
            mockClipService,
            mockQuickPasteService,
            mockContextFactory,
            A.Fake<IRemoteDatabaseService>(),
            mockMessenger,
            mockLogger);

        var previewVm = new PreviewPaneViewModel(mockMessenger);
        var searchVm = new SearchViewModel(
            mockSearchService,
            mockCollectionService,
            mockClipService,
            mockMessenger,
            new SearchResultsCache());

        // Create QuickPasteToolbarViewModel mock
        var mockQuickPasteServiceForToolbar = A.Fake<IQuickPasteService>();
        var mockConfigService = A.Fake<IConfigurationService>();
        var mockQuickPasteConfig = new PreferencesConfiguration();
        var mockConfig = new ClipMateConfiguration { Preferences = mockQuickPasteConfig };
        A.CallTo(() => mockConfigService.Configuration).Returns(mockConfig);
        var mockQuickPasteLogger = A.Fake<ILogger<QuickPasteToolbarViewModel>>();
        var quickPasteToolbarVm = new QuickPasteToolbarViewModel(
            mockQuickPasteServiceForToolbar,
            mockConfigService,
            mockMessenger,
            mockQuickPasteLogger);

        var mockServiceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => mockServiceProvider.GetService(typeof(ICollectionService))).Returns(mockCollectionService);
        A.CallTo(() => mockServiceProvider.GetService(typeof(IFolderService))).Returns(mockFolderService);
        var mockUpdateCheckService = A.Fake<IUpdateCheckService>();
        var mockAboutLogger = A.Fake<ILogger<AboutDialogViewModel>>();
        var aboutDialogViewModel = new AboutDialogViewModel(mockUpdateCheckService, mockAboutLogger);
        A.CallTo(() => mockServiceProvider.GetService(typeof(AboutDialogViewModel))).Returns(aboutDialogViewModel);
        A.CallTo(() => mockServiceProvider.GetService(typeof(IActiveWindowService))).Returns(A.Fake<IActiveWindowService>());

        var mockMenuMessenger = A.Fake<IMessenger>();
        var mockClipViewerWindowManager = A.Fake<IClipViewerWindowManager>();
        var mainMenuViewModel = new MainMenuViewModel(
            mockMenuMessenger,
            A.Fake<IUndoService>(),
            mockClipViewerWindowManager,
            A.Fake<IClipboardService>(),
            A.Fake<IAutoAppendService>(),
            A.Fake<IPowerPasteService>(),
            A.Fake<IQuickPasteService>(),
            mockServiceProvider);

        return new ExplorerWindowViewModel(
            collectionTreeVm,
            clipListVm,
            previewVm,
            searchVm,
            quickPasteToolbarVm,
            mainMenuViewModel,
            mockQuickPasteService,
            A.Fake<IPowerPasteService>(),
            mockCollectionService,
            mockFolderService,
            A.Fake<ITemplateService>(),
            mockMessenger, // Use the existing mockMessenger from earlier
            mockMainLogger);
    }

    private static IConfigurationService CreateConfigServiceMock()
    {
        var mock = A.Fake<IConfigurationService>();
        A.CallTo(() => mock.Configuration).Returns(new ClipMateConfiguration());
        return mock;
    }

    [Test]
    public async Task Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        await Assert.That(viewModel).IsNotNull();
        await Assert.That(viewModel.Title).IsEqualTo("ClipMate");
        await Assert.That(viewModel.WindowWidth).IsGreaterThan(0);
        await Assert.That(viewModel.WindowHeight).IsGreaterThan(0);
        await Assert.That(viewModel.IsBusy).IsFalse();
        await Assert.That(viewModel.StatusMessage).IsEmpty();
    }

    [Test]
    public async Task WindowWidth_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExplorerWindowViewModel.WindowWidth))
                propertyChangedRaised = true;
        };

        // Act - set to a DIFFERENT value
        viewModel.WindowWidth = 1500;

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(viewModel.WindowWidth).IsEqualTo(1500);
    }

    [Test]
    public async Task WindowHeight_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExplorerWindowViewModel.WindowHeight))
                propertyChangedRaised = true;
        };

        // Act - set to a DIFFERENT value
        viewModel.WindowHeight = 900;

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(viewModel.WindowHeight).IsEqualTo(900);
    }

    [Test]
    public async Task IsBusy_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExplorerWindowViewModel.IsBusy))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.IsBusy = true;

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(viewModel.IsBusy).IsTrue();
    }

    [Test]
    public async Task StatusMessage_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExplorerWindowViewModel.StatusMessage))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.StatusMessage = "Loading clips...";

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(viewModel.StatusMessage).IsEqualTo("Loading clips...");
    }

    [Test]
    public async Task SetStatus_ShouldUpdateStatusMessage()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SetStatus("Ready");

        // Assert
        await Assert.That(viewModel.StatusMessage).IsEqualTo("Ready");
    }

    [Test]
    public async Task SetBusy_WithTrue_ShouldSetIsBusyAndShowMessage()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SetBusy(true, "Processing...");

        // Assert
        await Assert.That(viewModel.IsBusy).IsTrue();
        await Assert.That(viewModel.StatusMessage).IsEqualTo("Processing...");
    }

    [Test]
    public async Task SetBusy_WithFalse_ShouldClearIsBusyAndMessage()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.SetBusy(true, "Processing...");

        // Act
        viewModel.SetBusy(false);

        // Assert
        await Assert.That(viewModel.IsBusy).IsFalse();
        await Assert.That(viewModel.StatusMessage).IsEmpty();
    }

    [Test]
    public async Task LeftPaneWidth_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExplorerWindowViewModel.LeftPaneWidth))
                propertyChangedRaised = true;
        };

        // Act - set to a DIFFERENT value
        viewModel.LeftPaneWidth = 300;

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(viewModel.LeftPaneWidth).IsEqualTo(300);
    }

    [Test]
    public async Task RightPaneWidth_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExplorerWindowViewModel.RightPaneWidth))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.RightPaneWidth = 350;

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(viewModel.RightPaneWidth).IsEqualTo(350);
    }
}
