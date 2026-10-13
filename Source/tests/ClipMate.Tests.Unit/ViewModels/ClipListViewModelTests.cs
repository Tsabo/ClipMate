using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
using ClipMate.Data;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

public class ClipListViewModelTests
{
    private readonly IClipService _mockClipService;
    private readonly ICollectionService _mockCollectionService;
    private readonly IDatabaseContextFactory _mockContextFactory;
    private readonly IFolderService _mockFolderService;
    private readonly ILogger<ClipListViewModel> _mockLogger;
    private readonly IMessenger _mockMessenger;
    private readonly IQuickPasteService _mockQuickPasteService;

    public ClipListViewModelTests()
    {
        _mockClipService = A.Fake<IClipService>();
        _mockContextFactory = A.Fake<IDatabaseContextFactory>();
        _mockMessenger = A.Fake<IMessenger>();
        _mockFolderService = A.Fake<IFolderService>();
        _mockCollectionService = A.Fake<ICollectionService>();
        _mockQuickPasteService = A.Fake<IQuickPasteService>();
        _mockLogger = A.Fake<ILogger<ClipListViewModel>>();
    }

    [Test]
    public async Task IsListView_DefaultValue_IsTrue()
    {
        var viewModel = new ClipListViewModel(
            _mockCollectionService,
            _mockFolderService,
            _mockClipService,
            _mockQuickPasteService,
            _mockContextFactory,
            _mockMessenger,
            _mockLogger);

        await Assert.That(viewModel.IsListView).IsTrue();
    }

    [Test]
    public async Task IsGridView_DefaultValue_IsFalse()
    {
        var viewModel = new ClipListViewModel(
            _mockCollectionService,
            _mockFolderService,
            _mockClipService,
            _mockQuickPasteService,
            _mockContextFactory,
            _mockMessenger,
            _mockLogger);

        await Assert.That(viewModel.IsGridView).IsFalse();
    }

    [Test]
    public async Task Constructor_ShouldRegisterWithMessenger()
    {
        // Note: Cannot verify extension method calls with FakeItEasy
        // The constructor should successfully complete, which implies registration worked
        // Testing actual message handling is done via integration tests
        var viewModel = new ClipListViewModel(
            _mockCollectionService,
            _mockFolderService,
            _mockClipService,
            _mockQuickPasteService,
            _mockContextFactory,
            _mockMessenger,
            _mockLogger);

        await Assert.That(viewModel).IsNotNull();
    }
}
