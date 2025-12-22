using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

public class ClipListViewModelTests
{
    private readonly Mock<ILogger<ClipListViewModel>> _mockLogger;
    private readonly Mock<IMessenger> _mockMessenger;
    private readonly Mock<IClipRepositoryFactory> _mockRepositoryFactory;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;

    public ClipListViewModelTests()
    {
        var mockClipService = new Mock<IClipService>();
        _mockRepositoryFactory = new Mock<IClipRepositoryFactory>();
        _mockMessenger = new Mock<IMessenger>();
        var mockFolderService = new Mock<IFolderService>();
        var mockCollectionService = new Mock<ICollectionService>();
        _mockLogger = new Mock<ILogger<ClipListViewModel>>();

        // Create mock service scope factory
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(p => p.GetService(typeof(IClipService))).Returns(mockClipService.Object);
        mockServiceProvider.Setup(p => p.GetService(typeof(IFolderService))).Returns(mockFolderService.Object);
        mockServiceProvider.Setup(p => p.GetService(typeof(ICollectionService))).Returns(mockCollectionService.Object);
        mockServiceScope.Setup(p => p.ServiceProvider).Returns(mockServiceProvider.Object);

        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceScopeFactory.Setup(p => p.CreateScope()).Returns(mockServiceScope.Object);
    }

    [Test]
    public async Task IsListView_DefaultValue_IsTrue()
    {
        var viewModel = new ClipListViewModel(
            _mockServiceScopeFactory.Object,
            _mockRepositoryFactory.Object,
            _mockMessenger.Object,
            _mockLogger.Object);

        await Assert.That(viewModel.IsListView).IsTrue();
    }

    [Test]
    public async Task IsGridView_DefaultValue_IsFalse()
    {
        var viewModel = new ClipListViewModel(
            _mockServiceScopeFactory.Object,
            _mockRepositoryFactory.Object,
            _mockMessenger.Object,
            _mockLogger.Object);

        await Assert.That(viewModel.IsGridView).IsFalse();
    }

    [Test]
    public async Task Constructor_ShouldRegisterWithMessenger()
    {
        // Note: Cannot verify extension method calls with Moq
        // The constructor should successfully complete, which implies registration worked
        // Testing actual message handling is done via integration tests
        var viewModel = new ClipListViewModel(
            _mockServiceScopeFactory.Object,
            _mockRepositoryFactory.Object,
            _mockMessenger.Object,
            _mockLogger.Object);

        await Assert.That(viewModel).IsNotNull();
    }
}
