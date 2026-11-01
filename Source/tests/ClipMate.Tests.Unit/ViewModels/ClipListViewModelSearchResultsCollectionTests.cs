using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using ClipMate.Tests.Unit.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The clip list's Collection column while it shows search results: where each result came from, named by the clip's
/// own Database.
/// </summary>
public class ClipListViewModelSearchResultsCollectionTests
{
    private const string _localKey = "db_local";
    private static readonly Guid _inboxId = Guid.NewGuid();
    private static readonly Guid _projectsId = Guid.NewGuid();
    private static readonly Guid _safeId = Guid.NewGuid();
    private static readonly RemoteDatabaseAddress _work = new(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
    private readonly IClipService _clipService = A.Fake<IClipService>();
    private readonly FakeDatabases _databases = new();
    private FakeDatabase _local = null!;

    [Before(Test)]
    public void Setup()
    {
        _local = _databases.AddLocal(_localKey, "My Clips");
        _local.Tree =
        [
            new CollectionEntry(_inboxId, null, "Inbox", CollectionEntryKind.Collection, 0),
            new CollectionEntry(_projectsId, _inboxId, "Projects", CollectionEntryKind.Folder, 0),
            new CollectionEntry(_safeId, null, "Safe", CollectionEntryKind.Collection, 1),
        ];
    }

    private ClipListViewModel CreateViewModel() => new(A.Fake<ICollectionService>(), A.Fake<IFolderService>(),
        _clipService, A.Fake<ICurrentClipWriter>(),
        A.Fake<IQuickPasteService>(),
        new PowerPasteStatus(), _databases, A.Fake<IRemoteDatabaseService>(), A.Fake<IMessenger>(), A.Fake<ILogger<ClipListViewModel>>());

    private Clip LocalClip(Guid collectionId, Guid? folderId = null)
    {
        var clip = new Clip { Id = Guid.NewGuid(), CollectionId = collectionId, FolderId = folderId };
        A.CallTo(() => _clipService.GetByIdAsync(_localKey, clip.Id, A<CancellationToken>._)).Returns(clip);
        return clip;
    }

    [Test]
    public async Task LoadClipsByIds_NamesEachClipsCollection()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var inbox = LocalClip(_inboxId);
        var safe = LocalClip(_safeId);

        // Act
        await viewModel.LoadClipsByIdsAsync([inbox.Id, safe.Id], _localKey);

        // Assert
        await Assert.That(viewModel.Clips.Select(p => p.CollectionDisplay)).IsEquivalentTo(new[] { "Inbox", "Safe" });
        await Assert.That(viewModel.ShowsSearchResults).IsTrue();
    }

    [Test]
    public async Task LoadClipsByIds_ClipInAFolder_NamesItsCollectionAndFolder()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var clip = LocalClip(_inboxId, _projectsId);

        // Act
        await viewModel.LoadClipsByIdsAsync([clip.Id], _localKey);

        // Assert
        await Assert.That(viewModel.Clips.Single().CollectionDisplay).IsEqualTo("Inbox › Projects");
    }

    [Test]
    public async Task Receive_RemoteSearchResultsSelected_NamesCollectionsFromThePeersTreeNotALocalOneWithTheSameIds()
    {
        // Arrange
        var work = _databases.AddRemote(new RemoteDatabase(_work, "Work", "LAPTOP", PeerConnectionStatus.Online, null));
        work.Tree =
        [
            new CollectionEntry(_inboxId, null, "Work Inbox", CollectionEntryKind.Collection, 0),
            new CollectionEntry(_projectsId, _inboxId, "Clients", CollectionEntryKind.Folder, 0),
        ];
        var viewModel = CreateViewModel();
        var clips = new List<Clip>
        {
            new() { Id = Guid.NewGuid(), CollectionId = _inboxId },
            new() { Id = Guid.NewGuid(), CollectionId = _inboxId, FolderId = _projectsId },
        };

        // Act
        viewModel.Receive(new RemoteSearchResultsSelectedEvent(_work, new RemoteSearchResult("report", clips, 2)));

        // Assert
        await Assert.That(viewModel.Clips.Select(p => p.CollectionDisplay)).IsEquivalentTo(new[] { "Work Inbox", "Work Inbox › Clients" });
        await Assert.That(viewModel.ShowsSearchResults).IsTrue();
    }

    [Test]
    public async Task Receive_RemoteSearchResultsSelected_PeerOfflineSinceTheSearch_ShowsTheMatchesWithNoCollection()
    {
        // Arrange
        var work = _databases.AddRemote(new RemoteDatabase(_work, "Work", "LAPTOP", PeerConnectionStatus.Online, null));
        work.Tree = [new CollectionEntry(_inboxId, null, "Work Inbox", CollectionEntryKind.Collection, 0)];
        work.IsAvailable = false;
        var viewModel = CreateViewModel();
        var clip = new Clip { Id = Guid.NewGuid(), CollectionId = _inboxId };

        // Act
        viewModel.Receive(new RemoteSearchResultsSelectedEvent(_work, new RemoteSearchResult("report", [clip], 1)));

        // Assert
        await Assert.That(viewModel.Clips.Single().Id).IsEqualTo(clip.Id);
        await Assert.That(viewModel.Clips.Single().CollectionDisplay).IsEmpty();
        await Assert.That(viewModel.SelectedClip).IsSameReferenceAs(viewModel.Clips.Single());
    }

    [Test]
    public async Task LoadAsync_ACollectionAfterSearchResults_NoLongerShowsSearchResults()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var clip = LocalClip(_inboxId);
        await viewModel.LoadClipsByIdsAsync([clip.Id], _localKey);

        // Act
        await viewModel.LoadAsync(_localKey, new ClipListScope.Collection(_inboxId));

        // Assert
        await Assert.That(viewModel.ShowsSearchResults).IsFalse();
    }

    [Test]
    public async Task Receive_RemoteSearchResultsSelected_PeerTreeNotAnsweringYet_ShowsTheMatchesAtOnceAndNamesThemWhenItArrives()
    {
        // Arrange
        var work = _databases.AddRemote(new RemoteDatabase(_work, "Work", "LAPTOP", PeerConnectionStatus.Online, null));
        work.Tree = [new CollectionEntry(_inboxId, null, "Work Inbox", CollectionEntryKind.Collection, 0)];
        var treeAnswer = new TaskCompletionSource();
        work.TreeReady = treeAnswer.Task;
        var viewModel = CreateViewModel();
        var clip = new Clip { Id = Guid.NewGuid(), CollectionId = _inboxId };
        var named = new TaskCompletionSource();
        clip.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Clip.CollectionDisplay))
                named.TrySetResult();
        };

        // Act
        viewModel.Receive(new RemoteSearchResultsSelectedEvent(_work, new RemoteSearchResult("report", [clip], 1)));
        var shownBeforeTheTree = viewModel.Clips.ToList();
        var selectedBeforeTheTree = viewModel.SelectedClip;
        treeAnswer.SetResult();
        await Task.WhenAny(named.Task, Task.Delay(TimeSpan.FromSeconds(10)));

        // Assert
        await Assert.That(shownBeforeTheTree).IsEquivalentTo(new[] { clip });
        await Assert.That(selectedBeforeTheTree).IsSameReferenceAs(clip);
        await Assert.That(named.Task.IsCompleted).IsTrue();
        await Assert.That(clip.CollectionDisplay).IsEqualTo("Work Inbox");
    }

    [Test]
    public async Task LoadClipsByIds_TreeFailsToRead_ShowsTheMatchesWithNoCollection()
    {
        // Arrange
        _local.TreeReady = Task.FromException(new InvalidOperationException("The tree is corrupt."));
        var viewModel = CreateViewModel();
        var inbox = LocalClip(_inboxId);
        var safe = LocalClip(_safeId);

        // Act
        await viewModel.LoadClipsByIdsAsync([inbox.Id, safe.Id], _localKey);

        // Assert
        await Assert.That(viewModel.Clips.Select(p => p.Id)).IsEquivalentTo(new[] { inbox.Id, safe.Id });
        await Assert.That(viewModel.Clips.Select(p => p.CollectionDisplay)).IsEquivalentTo(new[] { string.Empty, string.Empty });
        await Assert.That(viewModel.SelectedClip).IsSameReferenceAs(viewModel.Clips[0]);
    }

    [Test]
    public async Task Receive_RemoteSearchResultsSelected_DatabasesFailToAnswer_LogsTheFailureWithoutCrashing()
    {
        // Arrange
        var databases = A.Fake<IDatabases>();
        A.CallTo(() => databases.Get(A<string>._)).Throws(new InvalidOperationException("The Database list is being rebuilt."));
        var logger = new RecordingLogger<ClipListViewModel>();
        var viewModel = new ClipListViewModel(A.Fake<ICollectionService>(), A.Fake<IFolderService>(), _clipService,
            A.Fake<ICurrentClipWriter>(), A.Fake<IQuickPasteService>(), new PowerPasteStatus(), databases,
            A.Fake<IRemoteDatabaseService>(), A.Fake<IMessenger>(), logger);
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;

        // Act: an async void handler's failure reaches the synchronization context, where WPF's would crash the app
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            viewModel.Receive(new RemoteSearchResultsSelectedEvent(_work, new RemoteSearchResult("report", [new Clip { Id = Guid.NewGuid() }], 1)));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        // Assert
        await Assert.That(context.Failures).IsEmpty();
        await Assert.That(logger.Entries.Where(p => p.Level == LogLevel.Error).Select(p => p.Exception?.Message))
            .Contains("The Database list is being rebuilt.");
    }

    /// <summary>
    /// Runs what is posted to it at once, and keeps what that throws, as an async void method's failure is posted.
    /// </summary>
    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        public List<Exception> Failures { get; } = [];

        public override void Post(SendOrPostCallback d, object? state)
        {
            try
            {
                d(state);
            }
            catch (Exception ex)
            {
                Failures.Add(ex);
            }
        }
    }
}
