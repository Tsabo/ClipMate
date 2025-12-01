using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for MainWindowViewModel.
/// Following TDD: Tests written FIRST, then implementation.
/// </summary>
public class MainWindowViewModelTests
{
    private static Mock<IServiceScopeFactory> CreateMockServiceScopeFactory(Mock<ICollectionService> mockCollectionService,
        Mock<IFolderService> mockFolderService,
        Mock<IClipService> mockClipService,
        Mock<ISearchService> mockSearchService)
    {
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(ICollectionService))).Returns(mockCollectionService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IFolderService))).Returns(mockFolderService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IClipService))).Returns(mockClipService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(ISearchService))).Returns(mockSearchService.Object);
        mockServiceScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);

        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(mockServiceScope.Object);

        return mockServiceScopeFactory;
    }

    private MainWindowViewModel CreateViewModel()
    {
        var mockMessenger = new Mock<IMessenger>();
        var mockClipService = new Mock<IClipService>();
        var mockFolderService = new Mock<IFolderService>();
        var mockCollectionService = new Mock<ICollectionService>();
        var mockConfigurationService = new Mock<IConfigurationService>();
        var mockSearchService = new Mock<ISearchService>();
        var mockLogger = new Mock<ILogger<ClipListViewModel>>();
        var mockTreeLogger = new Mock<ILogger<CollectionTreeViewModel>>();
        var mockMainLogger = new Mock<ILogger<MainWindowViewModel>>();

        var mockServiceScopeFactory = CreateMockServiceScopeFactory(
            mockCollectionService, mockFolderService, mockClipService, mockSearchService);

        var collectionTreeVM = new CollectionTreeViewModel(
            mockServiceScopeFactory.Object,
            mockConfigurationService.Object,
            mockMessenger.Object,
            mockTreeLogger.Object);

        var clipListVM = new ClipListViewModel(
            mockServiceScopeFactory.Object,
            mockMessenger.Object,
            mockLogger.Object);

        var previewVM = new PreviewPaneViewModel(mockMessenger.Object);
        var searchVM = new SearchViewModel(mockServiceScopeFactory.Object, mockMessenger.Object);

        // Create QuickPasteToolbarViewModel mock
        var mockQuickPasteService = new Mock<IQuickPasteService>();
        var mockConfigService = new Mock<IConfigurationService>();
        var mockQuickPasteConfig = new PreferencesConfiguration();
        var mockConfig = new ClipMateConfiguration { Preferences = mockQuickPasteConfig };
        mockConfigService.Setup(x => x.Configuration).Returns(mockConfig);
        var mockQuickPasteLogger = new Mock<ILogger<QuickPasteToolbarViewModel>>();
        var quickPasteToolbarVm = new QuickPasteToolbarViewModel(
            mockQuickPasteService.Object,
            mockConfigService.Object,
            mockMessenger.Object,
            mockQuickPasteLogger.Object);

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(ICollectionService))).Returns(mockCollectionService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IFolderService))).Returns(mockFolderService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(mockServiceScopeFactory.Object);

        return new MainWindowViewModel(
            collectionTreeVM,
            clipListVM,
            previewVM,
            searchVM,
            quickPasteToolbarVm,
            mockServiceProvider.Object,
            mockMainLogger.Object);
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
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.WindowWidth))
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
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.WindowHeight))
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
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsBusy))
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
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.StatusMessage))
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
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.LeftPaneWidth))
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
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.RightPaneWidth))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.RightPaneWidth = 350;

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(viewModel.RightPaneWidth).IsEqualTo(350);
    }
}
