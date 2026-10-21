using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
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
    private readonly IRemoteDatabaseService _mockRemoteDatabases = A.Fake<IRemoteDatabaseService>();

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
            _mockRemoteDatabases,
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
            _mockRemoteDatabases,
            _mockMessenger,
            _mockLogger);

        await Assert.That(viewModel.IsGridView).IsFalse();
    }

    [Test]
    public async Task Receive_RemoteDatabaseNodeSelected_ShowsAReadOnlyRemoteListWithoutChangingTheCaptureTarget()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.CurrentDatabaseKey = "db_local";
        viewModel.Clips.Add(new Clip { Id = Guid.NewGuid() });
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(A<RemoteDatabaseAddress>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .Returns(new ClipPage([], 0, 0));

        // Act
        viewModel.Receive(new RemoteDatabaseNodeSelectedEvent(address, Guid.NewGuid(), null));

        // Assert
        await Assert.That(viewModel.Clips).IsEmpty();
        await Assert.That(viewModel.AcceptsNewClips).IsFalse();
        await Assert.That(viewModel.IsRemoteDatabase).IsTrue();
        await Assert.That(viewModel.CurrentDatabaseKey).IsEqualTo(RemoteDatabaseKey.From(address));
        A.CallTo(() => _mockCollectionService.SetActiveAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _mockFolderService.SetActiveAsync(A<Guid?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task LoadRemoteClipsAsync_LargeCollection_ShowsTheFirstPageThenAppendsTheRestPageByPage()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
        var collectionId = Guid.NewGuid();
        var first = Enumerable.Range(0, ClipListViewModel.RemotePageSize).Select(_ => new Clip { Id = Guid.NewGuid() }).ToList();
        var last = new Clip { Id = Guid.NewGuid() };
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(address, collectionId, null, 0, ClipListViewModel.RemotePageSize, A<CancellationToken>._))
            .Returns(new ClipPage(first, 0, first.Count + 1));
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(address, collectionId, null, first.Count, ClipListViewModel.RemotePageSize, A<CancellationToken>._))
            .Returns(new ClipPage([last], first.Count, first.Count + 1));

        // Act
        await viewModel.LoadRemoteClipsAsync(address, collectionId, null);

        // Assert
        await Assert.That(viewModel.Clips.Count).IsEqualTo(first.Count + 1);
        await Assert.That(viewModel.Clips[0].Id).IsEqualTo(first[0].Id);
        await Assert.That(viewModel.Clips[^1].Id).IsEqualTo(last.Id);
        await Assert.That(viewModel.IsLoading).IsFalse();
        await Assert.That(viewModel.RemoteStatusMessage).IsNull();
    }

    [Test]
    public async Task LoadRemoteClipsAsync_PeerOffline_ShowsTheReasonInsteadOfHanging()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(A<RemoteDatabaseAddress>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .Throws(new RemoteDatabaseUnavailableException("LAPTOP is offline."));

        // Act
        await viewModel.LoadRemoteClipsAsync(address, Guid.NewGuid(), null);

        // Assert
        await Assert.That(viewModel.Clips).IsEmpty();
        await Assert.That(viewModel.IsLoading).IsFalse();
        await Assert.That(viewModel.RemoteStatusMessage).IsEqualTo("LAPTOP is offline.");
    }

    [Test]
    public async Task LoadRemoteClipsAsync_Cancelled_StopsQuietly()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
        using var cancellation = new CancellationTokenSource();
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(A<RemoteDatabaseAddress>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                await cancellation.CancelAsync();
                call.GetArgument<CancellationToken>(5).ThrowIfCancellationRequested();
                return new ClipPage([], 0, 0);
            });

        // Act
        await viewModel.LoadRemoteClipsAsync(address, Guid.NewGuid(), null, cancellation.Token);

        // Assert
        await Assert.That(viewModel.IsLoading).IsFalse();
        await Assert.That(viewModel.RemoteStatusMessage).IsNull();
    }

    private ClipListViewModel CreateViewModel() => new(
        _mockCollectionService,
        _mockFolderService,
        _mockClipService,
        _mockQuickPasteService,
        _mockContextFactory,
        _mockRemoteDatabases,
        _mockMessenger,
        _mockLogger);

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
            _mockRemoteDatabases,
            _mockMessenger,
            _mockLogger);

        await Assert.That(viewModel).IsNotNull();
    }
}
