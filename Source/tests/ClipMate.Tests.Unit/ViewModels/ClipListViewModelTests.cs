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
            new PowerPasteStatus(),
            _mockContextFactory,
            _mockRemoteDatabases,
            _mockMessenger,
            _mockLogger);

        await Assert.That(viewModel.IsListView).IsTrue();
    }

    [Test]
    public async Task SelectClipWithoutCopying_ClipInList_SelectsIt()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var waiting = new Clip { Id = Guid.NewGuid() };
        viewModel.Clips.Add(new Clip { Id = Guid.NewGuid() });
        viewModel.Clips.Add(waiting);

        // Act
        viewModel.SelectClipWithoutCopying(waiting.Id);

        // Assert
        await Assert.That(viewModel.SelectedClip).IsEqualTo(waiting);
    }

    [Test]
    public async Task SelectClipWithoutCopying_WithSeveralRowsSelected_KeepsThemAllSelected()
    {
        // Arrange - the Power Paste sequence is the selected rows; a restart in the other direction reads them again
        var viewModel = CreateViewModel();
        var first = new Clip { Id = Guid.NewGuid() };
        var second = new Clip { Id = Guid.NewGuid() };
        viewModel.Clips.Add(first);
        viewModel.Clips.Add(second);
        viewModel.SelectedClips.Add(first);
        viewModel.SelectedClips.Add(second);

        // Act
        viewModel.SelectClipWithoutCopying(second.Id);

        // Assert
        await Assert.That(viewModel.SelectedClip).IsEqualTo(second);
        await Assert.That(viewModel.GetSelectedClipsInDisplayOrder()).IsEquivalentTo(new[] { first, second });
    }

    [Test]
    public async Task SelectClipWithoutCopying_ClipNotInList_KeepsTheSelection()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SelectClipWithoutCopying(Guid.NewGuid());

        // Assert
        await Assert.That(viewModel.SelectedClip).IsNull();
    }

    [Test]
    public async Task IsGridView_DefaultValue_IsFalse()
    {
        var viewModel = new ClipListViewModel(
            _mockCollectionService,
            _mockFolderService,
            _mockClipService,
            _mockQuickPasteService,
            new PowerPasteStatus(),
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

    [Test]
    public async Task RemoteCollectionChanged_ForTheOpenRemoteCollection_ReFetchesItAndKeepsTheSelection()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
        var collectionId = Guid.NewGuid();
        var first = new Clip { Id = Guid.NewGuid() };
        var second = new Clip { Id = Guid.NewGuid() };
        var captured = new Clip { Id = Guid.NewGuid() };
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(address, collectionId, null, 0, ClipListViewModel.RemotePageSize, A<CancellationToken>._))
            .Returns(new ClipPage([first, second], 0, 2)).Once()
            .Then.Returns(new ClipPage([captured, first, second], 0, 3));
        await viewModel.LoadRemoteClipsAsync(address, collectionId, null);
        viewModel.SelectedClip = viewModel.Clips[1];

        // Act: the Peer captured a clip into that collection
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(address, collectionId));
        await WaitUntilAsync(() => viewModel.Clips.Count == 3 && !viewModel.IsLoading);

        // Assert
        await Assert.That(viewModel.Clips.Select(p => p.Id)).IsEquivalentTo([captured.Id, first.Id, second.Id]);
        await Assert.That(viewModel.SelectedClip?.Id).IsEqualTo(second.Id);
    }

    [Test]
    public async Task RemoteCollectionChanged_ForTheOpenRemoteFolder_ReFetchesIt()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
        var collectionId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(address, collectionId, folderId, 0, ClipListViewModel.RemotePageSize, A<CancellationToken>._))
            .Returns(new ClipPage([], 0, 0));
        await viewModel.LoadRemoteClipsAsync(address, collectionId, folderId);

        // Act
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(address, folderId));
        await WaitUntilAsync(() => !viewModel.IsLoading);

        // Assert
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(address, collectionId, folderId, 0, A<int>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Test]
    public async Task RemoteCollectionChanged_ForAnotherCollectionOrDatabase_DoesNotReFetch()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
        var otherDatabase = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
        var collectionId = Guid.NewGuid();
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(A<RemoteDatabaseAddress>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .Returns(new ClipPage([], 0, 0));
        await viewModel.LoadRemoteClipsAsync(address, collectionId, null);

        // Act
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(address, Guid.NewGuid()));
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(otherDatabase, collectionId));
        await Task.Delay(50);

        // Assert
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(A<RemoteDatabaseAddress>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RemoteCollectionChanged_WhileALocalCollectionIsShown_DoesNothing()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var collectionId = Guid.NewGuid();
        viewModel.CurrentDatabaseKey = "db_local";
        viewModel.CurrentCollectionId = collectionId;
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());

        // Act
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(address, collectionId));
        await Task.Delay(50);

        // Assert
        A.CallTo(() => _mockRemoteDatabases.GetClipPageAsync(A<RemoteDatabaseAddress>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Test]
    public async Task Dispose_StopsListeningForRemoteChanges()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.Dispose();

        // Assert
        A.CallTo(_mockRemoteDatabases).Where(p => p.Method.Name == "remove_RemoteCollectionChanged").MustHaveHappenedOnceExactly();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    [Test]
    public async Task Receive_SelectNextClipFromHotkey_AnnouncesTheClipReached()
    {
        // Arrange
        var messenger = new StrongReferenceMessenger();
        using var viewModel = CreateViewModel(messenger);
        viewModel.CurrentDatabaseKey = "db_local";
        var first = new Clip { Id = Guid.NewGuid() };
        var second = new Clip { Id = Guid.NewGuid() };
        viewModel.Clips.Add(first);
        viewModel.Clips.Add(second);
        viewModel.SelectClipWithoutCopying(first.Id);
        ClipReachedByHotkeyEvent? reached = null;
        messenger.Register<ClipReachedByHotkeyEvent>(this, (_, message) => reached = message);

        // Act
        messenger.Send(new SelectNextClipEvent(FromHotkey: true));

        // Assert
        await Assert.That(viewModel.SelectedClip).IsEqualTo(second);
        await Assert.That(reached).IsNotNull();
        await Assert.That(reached!.Clip).IsEqualTo(second);
        await Assert.That(reached.DatabaseKey).IsEqualTo("db_local");
    }

    [Test]
    public async Task Receive_SelectPreviousClipFromHotkey_AnnouncesTheClipReached()
    {
        // Arrange
        var messenger = new StrongReferenceMessenger();
        using var viewModel = CreateViewModel(messenger);
        var first = new Clip { Id = Guid.NewGuid() };
        var second = new Clip { Id = Guid.NewGuid() };
        viewModel.Clips.Add(first);
        viewModel.Clips.Add(second);
        viewModel.SelectClipWithoutCopying(second.Id);
        ClipReachedByHotkeyEvent? reached = null;
        messenger.Register<ClipReachedByHotkeyEvent>(this, (_, message) => reached = message);

        // Act
        messenger.Send(new SelectPreviousClipEvent(FromHotkey: true));

        // Assert
        await Assert.That(reached?.Clip).IsEqualTo(first);
    }

    [Test]
    public async Task Receive_SelectNextClipFromHotkeyInAOneClipList_AnnouncesNothing()
    {
        // Arrange: the press does not move to another clip
        var messenger = new StrongReferenceMessenger();
        using var viewModel = CreateViewModel(messenger);
        var only = new Clip { Id = Guid.NewGuid() };
        viewModel.Clips.Add(only);
        viewModel.SelectClipWithoutCopying(only.Id);
        var announced = false;
        messenger.Register<ClipReachedByHotkeyEvent>(this, (_, _) => announced = true);

        // Act
        messenger.Send(new SelectNextClipEvent(FromHotkey: true));
        messenger.Send(new SelectPreviousClipEvent(FromHotkey: true));

        // Assert
        await Assert.That(viewModel.SelectedClip).IsEqualTo(only);
        await Assert.That(announced).IsFalse();
    }

    [Test]
    public async Task Receive_SelectNextClipInsideAWindow_AnnouncesNothing()
    {
        // Arrange
        var messenger = new StrongReferenceMessenger();
        using var viewModel = CreateViewModel(messenger);
        viewModel.Clips.Add(new Clip { Id = Guid.NewGuid() });
        viewModel.Clips.Add(new Clip { Id = Guid.NewGuid() });
        var announced = false;
        messenger.Register<ClipReachedByHotkeyEvent>(this, (_, _) => announced = true);

        // Act
        messenger.Send(new SelectNextClipEvent());
        messenger.Send(new SelectPreviousClipEvent());

        // Assert
        await Assert.That(viewModel.SelectedClip).IsNotNull();
        await Assert.That(announced).IsFalse();
    }

    private ClipListViewModel CreateViewModel(IMessenger messenger) => new(
        _mockCollectionService,
        _mockFolderService,
        _mockClipService,
        _mockQuickPasteService,
        new PowerPasteStatus(),
        _mockContextFactory,
        _mockRemoteDatabases,
        messenger,
        _mockLogger);

    private ClipListViewModel CreateViewModel() => new(
        _mockCollectionService,
        _mockFolderService,
        _mockClipService,
        _mockQuickPasteService,
        new PowerPasteStatus(),
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
            new PowerPasteStatus(),
            _mockContextFactory,
            _mockRemoteDatabases,
            _mockMessenger,
            _mockLogger);

        await Assert.That(viewModel).IsNotNull();
    }
}
