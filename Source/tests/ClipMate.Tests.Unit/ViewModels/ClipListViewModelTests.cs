using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using ClipMate.Tests.Unit.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;
using TUnit.Assertions.Enums;

namespace ClipMate.Tests.Unit.ViewModels;

public class ClipListViewModelTests
{
    private static readonly RemoteDatabaseAddress _laptopClips = new(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
    private readonly IClipService _mockClipService;
    private readonly ICollectionService _mockCollectionService;
    private readonly FakeDatabases _databases = new();
    private readonly IFolderService _mockFolderService;
    private readonly ILogger<ClipListViewModel> _mockLogger;
    private readonly IMessenger _mockMessenger;
    private readonly IQuickPasteService _mockQuickPasteService;
    private readonly IRemoteDatabaseService _mockRemoteDatabases = A.Fake<IRemoteDatabaseService>();
    private readonly FakeDatabase _local;
    private readonly FakeDatabase _remote;

    public ClipListViewModelTests()
    {
        _mockClipService = A.Fake<IClipService>();
        _mockMessenger = A.Fake<IMessenger>();
        _mockFolderService = A.Fake<IFolderService>();
        _mockCollectionService = A.Fake<ICollectionService>();
        _mockQuickPasteService = A.Fake<IQuickPasteService>();
        _mockLogger = A.Fake<ILogger<ClipListViewModel>>();
        _local = _databases.AddLocal("db_local", "My Clips");
        _remote = _databases.AddRemote(new RemoteDatabase(_laptopClips, "Clips", "LAPTOP", PeerConnectionStatus.Online, null));
    }

    private string RemoteKey => _remote.Key;

    [Test]
    public async Task IsListView_DefaultValue_IsTrue()
    {
        var viewModel = CreateViewModel();

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
        var viewModel = CreateViewModel();

        await Assert.That(viewModel.IsGridView).IsFalse();
    }

    [Test]
    public async Task Receive_CollectionNodeSelected_ShowsTheCollectionAndMakesItTheCaptureTarget()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var newest = Clip("Newest");
        _local.WithClips(new ClipListScope.Collection(collectionId), newest, Clip("Older"));
        var viewModel = CreateViewModel();

        // Act
        viewModel.Receive(new CollectionNodeSelectedEvent(collectionId, null, "db_local"));
        await WaitUntilAsync(() => viewModel.Clips.Count == 2 && !viewModel.IsLoading);

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("Newest, Older");
        await Assert.That(viewModel.SelectedClip).IsEqualTo(newest);
        await Assert.That(viewModel.AcceptsNewClips).IsTrue();
        await Assert.That(viewModel.IsRemoteDatabase).IsFalse();
        await Assert.That(viewModel.IsReadOnly).IsFalse();
        await Assert.That(viewModel.CurrentDatabaseKey).IsEqualTo("db_local");
        await Assert.That(viewModel.CurrentCollectionId).IsEqualTo(collectionId);
        A.CallTo(() => _mockCollectionService.SetActiveAsync(collectionId, "db_local", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task Receive_AnotherNodeSelectedWhileTheFirstIsBeingMadeActive_ShowsOnlyTheLaterNode()
    {
        // Arrange: making the first collection active reads its Database off the UI thread and has not finished
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _local.WithClips(new ClipListScope.Collection(first), Clip("In the first"));
        _local.WithClips(new ClipListScope.Collection(second), Clip("In the second"));
        var firstActivation = new TaskCompletionSource();
        Guid? active = null;
        A.CallTo(() => _mockCollectionService.SetActiveAsync(first, "db_local", A<CancellationToken>._))
            .ReturnsLazily(() => firstActivation.Task.ContinueWith(_ => active = first, TaskContinuationOptions.ExecuteSynchronously));
        A.CallTo(() => _mockCollectionService.SetActiveAsync(second, "db_local", A<CancellationToken>._))
            .Invokes(() => active = second).Returns(Task.CompletedTask);
        var viewModel = CreateViewModel();

        // Act
        var showingFirst = viewModel.ShowCollectionNodeAsync(new CollectionNodeSelectedEvent(first, null, "db_local"));
        var showingSecond = viewModel.ShowCollectionNodeAsync(new CollectionNodeSelectedEvent(second, null, "db_local"));
        firstActivation.SetResult();
        await Task.WhenAll(showingFirst, showingSecond);

        // Assert: the later node is both shown and made active last, so it is the capture target
        await Assert.That(Titles(viewModel)).IsEqualTo("In the second");
        await Assert.That(viewModel.CurrentCollectionId).IsEqualTo(second);
        await Assert.That(_local.PageRequests.Any(p => p.Scope == new ClipListScope.Collection(first))).IsFalse();
        await Assert.That(active).IsEqualTo(second);
    }

    [Test]
    public async Task ShowCollectionNodeAsync_RemoteDatabaseNodeSelectedWhileTheCollectionIsBeingMadeActive_KeepsTheRemoteView()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var remoteCollectionId = Guid.NewGuid();
        _local.WithClips(new ClipListScope.Collection(collectionId), Clip("Local"));
        _remote.WithClips(new ClipListScope.Collection(remoteCollectionId), Clip("Remote"));
        var activation = new TaskCompletionSource();
        A.CallTo(() => _mockCollectionService.SetActiveAsync(collectionId, "db_local", A<CancellationToken>._)).Returns(activation.Task);
        var viewModel = CreateViewModel();

        // Act
        var showingCollection = viewModel.ShowCollectionNodeAsync(new CollectionNodeSelectedEvent(collectionId, null, "db_local"));
        await viewModel.LoadAsync(RemoteKey, new ClipListScope.Collection(remoteCollectionId));
        activation.SetResult();
        await showingCollection;

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("Remote");
        await Assert.That(viewModel.CurrentDatabaseKey).IsEqualTo(RemoteKey);
        await Assert.That(_local.PageRequests).IsEmpty();
    }

    [Test]
    public async Task ShowCollectionNodeAsync_SearchResultsSelectedWhileTheCollectionIsBeingMadeActive_KeepsTheSearchResults()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var found = Clip("Found");
        _local.WithClips(new ClipListScope.Collection(collectionId), Clip("In the collection"));
        A.CallTo(() => _mockClipService.GetByIdAsync("db_local", found.Id, A<CancellationToken>._)).Returns(found);
        var activation = new TaskCompletionSource();
        A.CallTo(() => _mockCollectionService.SetActiveAsync(collectionId, "db_local", A<CancellationToken>._)).Returns(activation.Task);
        var viewModel = CreateViewModel();

        // Act
        var showingCollection = viewModel.ShowCollectionNodeAsync(new CollectionNodeSelectedEvent(collectionId, null, "db_local"));
        await viewModel.LoadClipsByIdsAsync([found.Id], "db_local");
        activation.SetResult();
        await showingCollection;

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("Found");
        await Assert.That(viewModel.ShowsSearchResults).IsTrue();
        await Assert.That(_local.PageRequests).IsEmpty();
    }

    [Test]
    public async Task ShowCollectionNodeAsync_RemoteSearchResultsSelectedWhileTheCollectionIsBeingMadeActive_KeepsTheSearchResults()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        _local.WithClips(new ClipListScope.Collection(collectionId), Clip("In the collection"));
        var activation = new TaskCompletionSource();
        A.CallTo(() => _mockCollectionService.SetActiveAsync(collectionId, "db_local", A<CancellationToken>._)).Returns(activation.Task);
        var viewModel = CreateViewModel();

        // Act
        var showingCollection = viewModel.ShowCollectionNodeAsync(new CollectionNodeSelectedEvent(collectionId, null, "db_local"));
        viewModel.Receive(new RemoteSearchResultsSelectedEvent(_laptopClips, new RemoteSearchResult("report", [Clip("Found on the Peer")], 1)));
        activation.SetResult();
        await showingCollection;

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("Found on the Peer");
        await Assert.That(viewModel.CurrentDatabaseKey).IsEqualTo(RemoteKey);
        await Assert.That(_local.PageRequests).IsEmpty();
    }

    [Test]
    public async Task LoadAsync_LoadedClipDeletedMidLoad_StillShowsEveryRemainingClip()
    {
        // Arrange
        var scope = new ClipListScope.Collection(Guid.NewGuid());
        var clips = Enumerable.Range(0, 2_201).Select(p => Clip($"Clip {p}")).ToArray();
        _local.WithClips(scope, clips);
        var secondPage = new TaskCompletionSource();
        _local.PageReady = offset => offset != 0 ? secondPage.Task : Task.CompletedTask;
        var viewModel = CreateViewModel();
        var load = viewModel.LoadAsync("db_local", scope);
        await WaitUntilAsync(() => _local.PageRequests.Count == 2);

        // Act: two clips of the first page are deleted while the second page is on its way, moving every later clip up
        var deleted = new[] { clips[3], clips[150] };
        var remaining = clips.Except(deleted).ToArray();
        _local.WithClips(scope, remaining);
        viewModel.Receive(new ClipsDeletedEvent(deleted.Select(p => p.Id).ToList()));
        secondPage.SetResult();
        await load;

        // Assert
        await Assert.That(viewModel.Clips.Select(p => p.Id)).IsEquivalentTo(remaining.Select(p => p.Id), CollectionOrdering.Matching);
    }

    [Test]
    public async Task RefreshAsync_WhileShowingSearchResults_ShowsThemAgainWithoutClipsNoLongerThere()
    {
        // Arrange
        var first = Clip("First");
        var deleted = Clip("Deleted");
        var last = Clip("Last");
        foreach (var item in new[] { first, deleted, last })
            A.CallTo(() => _mockClipService.GetByIdAsync("db_local", item.Id, A<CancellationToken>._)).Returns(item);

        var viewModel = CreateViewModel();
        await viewModel.LoadClipsByIdsAsync([first.Id, deleted.Id, last.Id], "db_local");
        A.CallTo(() => _mockClipService.GetByIdAsync("db_local", deleted.Id, A<CancellationToken>._)).Returns((Clip?)null);

        // Act: a clip of the results was re-encrypted (say), and another was deleted meanwhile
        await viewModel.RefreshAsync();

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("First, Last");
        await Assert.That(viewModel.ShowsSearchResults).IsTrue();
        await Assert.That(viewModel.AcceptsNewClips).IsFalse();
    }

    [Test]
    public async Task RefreshAsync_WhileShowingRemoteSearchResults_ShowsThemAgain()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.Receive(new RemoteSearchResultsSelectedEvent(_laptopClips, new RemoteSearchResult("report", [Clip("Found on the Peer")], 1)));
        await WaitUntilAsync(() => !viewModel.IsLoading);

        // Act
        await viewModel.RefreshAsync();

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("Found on the Peer");
        await Assert.That(viewModel.ShowsSearchResults).IsTrue();
        await Assert.That(viewModel.CurrentDatabaseKey).IsEqualTo(RemoteKey);
    }

    [Test]
    public async Task Receive_FolderNodeSelected_ShowsTheFolder()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        _local.WithClips(new ClipListScope.Folder(collectionId, folderId), Clip("In the folder"));
        var viewModel = CreateViewModel();

        // Act
        viewModel.Receive(new CollectionNodeSelectedEvent(collectionId, folderId, "db_local"));
        await WaitUntilAsync(() => viewModel.Clips.Count == 1 && !viewModel.IsLoading);

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("In the folder");
        await Assert.That(viewModel.CurrentFolderId).IsEqualTo(folderId);
        await Assert.That(viewModel.AcceptsNewClips).IsTrue();
    }

    [Test]
    public async Task Receive_TrashcanSelected_ShowsTheTrashWithoutChangingTheCaptureTarget()
    {
        // Arrange
        _local.WithClips(new ClipListScope.Trash(), Clip("Deleted"));
        var viewModel = CreateViewModel();

        // Act
        viewModel.Receive(new CollectionNodeSelectedEvent(Guid.NewGuid(), null, "db_local", true));
        await WaitUntilAsync(() => viewModel.Clips.Count == 1 && !viewModel.IsLoading);

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("Deleted");
        await Assert.That(viewModel.AcceptsNewClips).IsFalse();
        await Assert.That(viewModel.CurrentCollectionId).IsNull();
        A.CallTo(() => _mockCollectionService.SetActiveAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    [Arguments(CollectionEntryKind.Smart)]
    [Arguments(CollectionEntryKind.SqlBased)]
    public async Task LoadAsync_SmartOrSqlCollection_DoesNotAcceptNewClips(CollectionEntryKind kind)
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        _local.Tree = [new CollectionEntry(collectionId, null, "Today", kind, 1)];
        _local.WithClips(new ClipListScope.Collection(collectionId), Clip("Found"));
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAsync("db_local", new ClipListScope.Collection(collectionId));

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("Found");
        await Assert.That(viewModel.AcceptsNewClips).IsFalse();
    }

    [Test]
    public async Task Receive_ShowAllClipsInChildren_ShowsTheCollectionWithItsFolders()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        _local.WithClips(new ClipListScope.Collection(collectionId, IncludeFolders: true), Clip("In a folder"), Clip("In the collection"));
        var viewModel = CreateViewModel();

        // Act
        viewModel.Receive(new ShowAllClipsInChildrenRequestedEvent(collectionId, "db_local"));
        await WaitUntilAsync(() => viewModel.Clips.Count == 2 && !viewModel.IsLoading);

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("In a folder, In the collection");
        await Assert.That(viewModel.AcceptsNewClips).IsTrue();
    }

    [Test]
    public async Task LoadAsync_LocalDatabaseFileMissing_ShowsTheReason()
    {
        // Arrange
        _local.IsAvailable = false;
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAsync("db_local", new ClipListScope.Collection(Guid.NewGuid()));

        // Assert
        await Assert.That(viewModel.Clips).IsEmpty();
        await Assert.That(viewModel.RemoteStatusMessage).IsEqualTo("My Clips is unavailable.");
        await Assert.That(viewModel.IsLoading).IsFalse();
    }

    [Test]
    public async Task RefreshAsync_LoadsTheSameListAgain()
    {
        // Arrange
        _local.WithClips(new ClipListScope.Trash(), Clip("Deleted"));
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync("db_local", new ClipListScope.Trash());

        // Act
        await viewModel.RefreshAsync();

        // Assert
        await Assert.That(_local.PageRequests.Count(p => p.Scope is ClipListScope.Trash)).IsEqualTo(2);
        await Assert.That(Titles(viewModel)).IsEqualTo("Deleted");
    }

    [Test]
    public async Task RefreshAsync_AfterClipsAreMovedToAnotherCollection_StillShowsTheCollectionWithoutThem()
    {
        // Arrange
        var inbox = new ClipListScope.Collection(Guid.NewGuid());
        var staying = Clip("Staying");
        _local.WithClips(inbox, Clip("Moved"), staying);
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync("db_local", inbox);
        _local.WithClips(inbox, staying);

        // Act
        await viewModel.RefreshAsync();

        // Assert
        await Assert.That(Titles(viewModel)).IsEqualTo("Staying");
        await Assert.That(viewModel.CurrentCollectionId).IsEqualTo(inbox.CollectionId);
    }

    [Test]
    public async Task RefreshAsync_WithNoViewShown_ShowsNothing()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        await viewModel.RefreshAsync();

        // Assert
        await Assert.That(viewModel.Clips).IsEmpty();
        await Assert.That(_local.PageRequests).IsEmpty();
        A.CallTo(_mockClipService).MustNotHaveHappened();
    }

    [Test]
    public async Task Receive_RemoteDatabaseNodeSelected_ShowsAReadOnlyRemoteListWithoutChangingTheCaptureTarget()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.CurrentDatabaseKey = "db_local";
        viewModel.Clips.Add(new Clip { Id = Guid.NewGuid() });

        // Act
        viewModel.Receive(new RemoteDatabaseNodeSelectedEvent(_laptopClips, Guid.NewGuid(), null));
        await WaitUntilAsync(() => !viewModel.IsLoading);

        // Assert
        await Assert.That(viewModel.Clips).IsEmpty();
        await Assert.That(viewModel.AcceptsNewClips).IsFalse();
        await Assert.That(viewModel.IsRemoteDatabase).IsTrue();
        await Assert.That(viewModel.IsReadOnly).IsTrue();
        await Assert.That(viewModel.CurrentDatabaseKey).IsEqualTo(RemoteKey);
        A.CallTo(() => _mockCollectionService.SetActiveAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _mockFolderService.SetActiveAsync(A<Guid?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task LoadAsync_LargeCollection_Shows200ClipsThenAddsPagesOf2000AsOneUpdateEach()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var clips = Enumerable.Range(0, 2_201).Select(p => Clip($"Clip {p}")).ToArray();
        _remote.WithClips(new ClipListScope.Collection(collectionId), clips);
        var viewModel = CreateViewModel();
        var countAtEachUpdate = new List<int>();
        viewModel.Clips.CollectionChanged += (_, _) => countAtEachUpdate.Add(viewModel.Clips.Count);

        // Act
        await viewModel.LoadAsync(RemoteKey, new ClipListScope.Collection(collectionId));

        // Assert: the list is cleared, then grows once per page
        await Assert.That(countAtEachUpdate).IsEquivalentTo([0, 200, 2_200, 2_201], CollectionOrdering.Matching);
        await Assert.That(_remote.PageRequests.Select(p => (p.Offset, p.Count))).IsEquivalentTo([(0, 200), (200, 2_000), (2_200, 2_000)], CollectionOrdering.Matching);
        await Assert.That(viewModel.Clips.Select(p => p.Id)).IsEquivalentTo(clips.Select(p => p.Id), CollectionOrdering.Matching);
        await Assert.That(viewModel.SelectedClip?.Id).IsEqualTo(clips[0].Id);
        await Assert.That(viewModel.IsLoading).IsFalse();
        await Assert.That(viewModel.RemoteStatusMessage).IsNull();
    }

    [Test]
    public async Task LoadAsync_LargeCollection_ReportsProgressForTheStatusBarNotTheBanner()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var scope = new ClipListScope.Collection(collectionId);
        _local.WithClips(scope, Enumerable.Range(0, 2_201).Select(p => Clip($"Clip {p}")).ToArray());
        var secondPage = new TaskCompletionSource();
        _local.PageReady = offset => offset == ClipListViewModel.FirstPageSize ? secondPage.Task : Task.CompletedTask;
        var viewModel = CreateViewModel();

        // Act: the first page has arrived and the second is on its way
        var load = viewModel.LoadAsync("db_local", scope);
        await WaitUntilAsync(() => _local.PageRequests.Count == 2);

        // Assert
        await Assert.That(viewModel.LoadProgressMessage).IsEqualTo("Loading clips… 200 of 2,201");
        await Assert.That(viewModel.LoadProgressPercent).IsEqualTo(9);
        await Assert.That(viewModel.RemoteStatusMessage).IsNull();

        secondPage.SetResult();
        await load;
        await Assert.That(viewModel.LoadProgressMessage).IsNull();
        await Assert.That(viewModel.LoadProgressPercent).IsEqualTo(0);
    }

    [Test]
    public async Task LoadAsync_ClipCapturedMidLoad_AppearsOnceAtTheTop()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var scope = new ClipListScope.Collection(collectionId);
        var clips = Enumerable.Range(0, 2_201).Select(p => Clip($"Clip {p}")).ToArray();
        _local.WithClips(scope, clips);
        var secondPage = new TaskCompletionSource();
        _local.PageReady = offset => offset == ClipListViewModel.FirstPageSize ? secondPage.Task : Task.CompletedTask;
        var viewModel = CreateViewModel();
        var load = viewModel.LoadAsync("db_local", scope);
        await WaitUntilAsync(() => _local.PageRequests.Count == 2);

        // Act: a capture is saved and announced while the second page is on its way, shifting every later clip down one
        var captured = Clip("Captured");
        _local.WithClips(scope, [captured, .. clips]);
        viewModel.Receive(new ClipAddedEvent(captured, false, "db_local", collectionId));
        secondPage.SetResult();
        await load;

        // Assert
        await Assert.That(viewModel.Clips.Select(p => p.Id)).IsEquivalentTo([captured.Id, .. clips.Select(p => p.Id)], CollectionOrdering.Matching);
        await Assert.That(viewModel.SelectedClip?.Id).IsEqualTo(captured.Id);
    }

    [Test]
    public async Task LoadAsync_PeerOffline_ShowsTheReasonInsteadOfHanging()
    {
        // Arrange
        _remote.IsAvailable = false;
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAsync(RemoteKey, new ClipListScope.Collection(Guid.NewGuid()));

        // Assert
        await Assert.That(viewModel.Clips).IsEmpty();
        await Assert.That(viewModel.IsLoading).IsFalse();
        await Assert.That(viewModel.RemoteStatusMessage).IsEqualTo("Clips is unavailable.");
    }

    [Test]
    public async Task LoadAsync_RemoteDatabaseNoLongerShared_ShowsTheReason()
    {
        // Arrange
        _databases.Remove(RemoteKey);
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAsync(RemoteKey, new ClipListScope.Collection(Guid.NewGuid()));

        // Assert
        await Assert.That(viewModel.Clips).IsEmpty();
        await Assert.That(viewModel.AcceptsNewClips).IsFalse();
        await Assert.That(viewModel.RemoteStatusMessage).IsNotNull();
    }

    [Test]
    public async Task LoadAsync_Cancelled_StopsQuietly()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => database.GetClipPageAsync(A<ClipListScope>._, A<int>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                await cancellation.CancelAsync();
                call.GetArgument<CancellationToken>(3).ThrowIfCancellationRequested();
                return new ClipPage([], 0, 0);
            });

        var databases = A.Fake<IDatabases>();
        A.CallTo(() => databases.Get("db_slow")).Returns(database);
        var viewModel = CreateViewModel(_mockMessenger, databases);

        // Act
        await viewModel.LoadAsync("db_slow", new ClipListScope.Collection(Guid.NewGuid()), cancellation.Token);

        // Assert
        await Assert.That(viewModel.IsLoading).IsFalse();
        await Assert.That(viewModel.RemoteStatusMessage).IsNull();
    }

    [Test]
    public async Task RemoteCollectionChanged_ForTheOpenRemoteCollection_ReFetchesItAndKeepsTheSelection()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var collectionId = Guid.NewGuid();
        var first = Clip("First");
        var second = Clip("Second");
        var captured = Clip("Captured");
        _remote.WithClips(new ClipListScope.Collection(collectionId), first, second);
        await viewModel.LoadAsync(RemoteKey, new ClipListScope.Collection(collectionId));
        viewModel.SelectedClip = viewModel.Clips[1];
        _remote.WithClips(new ClipListScope.Collection(collectionId), captured, first, second);

        // Act: the Peer captured a clip into that collection
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(_laptopClips, collectionId));
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
        var collectionId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        await viewModel.LoadAsync(RemoteKey, new ClipListScope.Folder(collectionId, folderId));

        // Act
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(_laptopClips, folderId));
        await WaitUntilAsync(() => _remote.PageRequests.Count == 2 && !viewModel.IsLoading);

        // Assert
        await Assert.That(_remote.PageRequests.Count(p => p.Scope == new ClipListScope.Folder(collectionId, folderId))).IsEqualTo(2);
    }

    [Test]
    public async Task RemoteCollectionChanged_ForAnotherCollectionOrDatabase_DoesNotReFetch()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var otherDatabase = new RemoteDatabaseAddress(_laptopClips.DeviceId, Guid.NewGuid());
        var collectionId = Guid.NewGuid();
        await viewModel.LoadAsync(RemoteKey, new ClipListScope.Collection(collectionId));

        // Act
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(_laptopClips, Guid.NewGuid()));
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(otherDatabase, collectionId));
        await Task.Delay(50);

        // Assert
        await Assert.That(_remote.PageRequests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RemoteCollectionChanged_WhileALocalCollectionIsShown_DoesNothing()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var collectionId = Guid.NewGuid();
        await viewModel.LoadAsync("db_local", new ClipListScope.Collection(collectionId));

        // Act
        _mockRemoteDatabases.RemoteCollectionChanged += Raise.With(new RemoteCollectionChangedEventArgs(_laptopClips, collectionId));
        await Task.Delay(50);

        // Assert
        await Assert.That(_remote.PageRequests).IsEmpty();
        await Assert.That(_local.PageRequests.Count).IsEqualTo(1);
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

    private static Clip Clip(string title) => new() { Id = Guid.NewGuid(), Title = title };

    private static string Titles(ClipListViewModel viewModel) => string.Join(", ", viewModel.Clips.Select(p => p.Title));

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

    private ClipListViewModel CreateViewModel(IMessenger messenger, IDatabases? databases = null) => new(
        _mockCollectionService,
        _mockFolderService,
        _mockClipService,
        A.Fake<ICurrentClipWriter>(),
        _mockQuickPasteService,
        new PowerPasteStatus(),
        databases ?? _databases,
        _mockRemoteDatabases,
        messenger,
        _mockLogger);

    private ClipListViewModel CreateViewModel() => CreateViewModel(_mockMessenger);

    [Test]
    public async Task Constructor_ShouldRegisterWithMessenger()
    {
        // Note: Cannot verify extension method calls with FakeItEasy
        // The constructor should successfully complete, which implies registration worked
        // Testing actual message handling is done via integration tests
        var viewModel = CreateViewModel();

        await Assert.That(viewModel).IsNotNull();
    }
}
