using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for ExplorerWindowViewModel.
/// Following TDD: Tests written FIRST, then implementation.
/// </summary>
public class ExplorerWindowViewModelTests
{
    private ExplorerWindowViewModel CreateViewModel()
    {
        var mockMessenger = new Mock<IMessenger>();
        var mockClipService = new Mock<IClipService>();
        var mockFolderService = new Mock<IFolderService>();
        var mockCollectionService = new Mock<ICollectionService>();
        var mockConfigurationService = new Mock<IConfigurationService>();
        var mockSearchService = new Mock<ISearchService>();
        var mockContextFactory = new Mock<IDatabaseContextFactory>();
        var mockQuickPasteService = new Mock<IQuickPasteService>();
        var mockLogger = new Mock<ILogger<ClipListViewModel>>();
        var mockTreeLogger = new Mock<ILogger<CollectionTreeViewModel>>();
        var mockMainLogger = new Mock<ILogger<ExplorerWindowViewModel>>();
        var mockCollectionTreeBuilder = new Mock<ICollectionTreeBuilder>();

        var collectionTreeVm = new CollectionTreeViewModel(
            mockCollectionService.Object,
            mockFolderService.Object,
            mockClipService.Object,
            mockConfigurationService.Object,
            mockMessenger.Object,
            mockCollectionTreeBuilder.Object,
            mockTreeLogger.Object,
            new SearchResultsCache());

        var clipListVm = new ClipListViewModel(
            mockCollectionService.Object,
            mockFolderService.Object,
            mockClipService.Object,
            mockQuickPasteService.Object,
            mockContextFactory.Object,
            mockMessenger.Object,
            mockLogger.Object);

        var previewVm = new PreviewPaneViewModel(mockMessenger.Object);
        var searchVm = new SearchViewModel(
            mockSearchService.Object,
            mockCollectionService.Object,
            mockClipService.Object,
            mockMessenger.Object,
            new SearchResultsCache());

        // Create QuickPasteToolbarViewModel mock
        var mockQuickPasteServiceForToolbar = new Mock<IQuickPasteService>();
        var mockConfigService = new Mock<IConfigurationService>();
        var mockQuickPasteConfig = new PreferencesConfiguration();
        var mockConfig = new ClipMateConfiguration { Preferences = mockQuickPasteConfig };
        mockConfigService.Setup(p => p.Configuration).Returns(mockConfig);
        var mockQuickPasteLogger = new Mock<ILogger<QuickPasteToolbarViewModel>>();
        var quickPasteToolbarVm = new QuickPasteToolbarViewModel(
            mockQuickPasteServiceForToolbar.Object,
            mockConfigService.Object,
            mockMessenger.Object,
            mockQuickPasteLogger.Object);

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(p => p.GetService(typeof(ICollectionService))).Returns(mockCollectionService.Object);
        mockServiceProvider.Setup(p => p.GetService(typeof(IFolderService))).Returns(mockFolderService.Object);
        var mockUpdateCheckService = new Mock<IUpdateCheckService>();
        var mockAboutLogger = new Mock<ILogger<AboutDialogViewModel>>();
        var aboutDialogViewModel = new AboutDialogViewModel(mockUpdateCheckService.Object, mockAboutLogger.Object);
        mockServiceProvider.Setup(p => p.GetService(typeof(AboutDialogViewModel))).Returns(aboutDialogViewModel);
        mockServiceProvider.Setup(p => p.GetService(typeof(IActiveWindowService))).Returns(new Mock<IActiveWindowService>().Object);

        var mockMenuMessenger = new Mock<IMessenger>();
        var mockClipViewerWindowManager = new Mock<IClipViewerWindowManager>();
        var mainMenuViewModel = new MainMenuViewModel(
            mockMenuMessenger.Object,
            new Mock<IUndoService>().Object,
            mockClipViewerWindowManager.Object,
            new Mock<IClipboardService>().Object,
            mockServiceProvider.Object);

        return new ExplorerWindowViewModel(
            collectionTreeVm,
            clipListVm,
            previewVm,
            searchVm,
            quickPasteToolbarVm,
            mainMenuViewModel,
            mockQuickPasteService.Object,
            new Mock<IPowerPasteService>().Object,
            mockCollectionService.Object,
            mockFolderService.Object,
            new Mock<ITemplateService>().Object,
            mockMessenger.Object, // Use the existing mockMessenger.Object from earlier
            mockMainLogger.Object);
    }

    private static Mock<IConfigurationService> CreateConfigServiceMock()
    {
        var mock = new Mock<IConfigurationService>();
        mock.Setup(p => p.Configuration).Returns(new ClipMateConfiguration());
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
