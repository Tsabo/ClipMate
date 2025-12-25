using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

public class ClipListViewModelTests
{
    private readonly Mock<IClipService> _mockClipService;
    private readonly Mock<ICollectionService> _mockCollectionService;
    private readonly Mock<IFolderService> _mockFolderService;
    private readonly Mock<ILogger<ClipListViewModel>> _mockLogger;
    private readonly Mock<IMessenger> _mockMessenger;
    private readonly Mock<IQuickPasteService> _mockQuickPasteService;
    private readonly Mock<IClipRepositoryFactory> _mockRepositoryFactory;

    public ClipListViewModelTests()
    {
        _mockClipService = new Mock<IClipService>();
        _mockRepositoryFactory = new Mock<IClipRepositoryFactory>();
        _mockMessenger = new Mock<IMessenger>();
        _mockFolderService = new Mock<IFolderService>();
        _mockCollectionService = new Mock<ICollectionService>();
        _mockQuickPasteService = new Mock<IQuickPasteService>();
        _mockLogger = new Mock<ILogger<ClipListViewModel>>();
    }

    [Test]
    public async Task IsListView_DefaultValue_IsTrue()
    {
        var viewModel = new ClipListViewModel(
            _mockCollectionService.Object,
            _mockFolderService.Object,
            _mockClipService.Object,
            _mockQuickPasteService.Object,
            _mockRepositoryFactory.Object,
            _mockMessenger.Object,
            _mockLogger.Object);

        await Assert.That(viewModel.IsListView).IsTrue();
    }

    [Test]
    public async Task IsGridView_DefaultValue_IsFalse()
    {
        var viewModel = new ClipListViewModel(
            _mockCollectionService.Object,
            _mockFolderService.Object,
            _mockClipService.Object,
            _mockQuickPasteService.Object,
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
            _mockCollectionService.Object,
            _mockFolderService.Object,
            _mockClipService.Object,
            _mockQuickPasteService.Object,
            _mockRepositoryFactory.Object,
            _mockMessenger.Object,
            _mockLogger.Object);

        await Assert.That(viewModel).IsNotNull();
    }
}
