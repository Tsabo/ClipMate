using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Remote Database nodes in the collection tree.
/// </summary>
public class CollectionTreeViewModelRemoteDatabaseTests
{
    private static readonly DeviceId _laptop = DeviceId.FromCertificateBytes([1]);
    private static readonly RemoteDatabaseAddress _work = new(_laptop, Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private ICollectionService _collectionService = null!;
    private StrongReferenceMessenger _messenger = null!;
    private List<RemoteDatabase> _remoteDatabases = null!;
    private IRemoteDatabaseTreeBuilder _remoteTreeBuilder = null!;
    private CollectionTreeViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _collectionService = A.Fake<ICollectionService>();
        _messenger = new StrongReferenceMessenger();
        _remoteDatabases = [];
        var remoteDatabaseService = A.Fake<IRemoteDatabaseService>();
        A.CallTo(() => remoteDatabaseService.GetRemoteDatabases()).ReturnsLazily(() => _remoteDatabases.ToList());
        _remoteTreeBuilder = A.Fake<IRemoteDatabaseTreeBuilder>();

        var localTree = A.Fake<ICollectionTreeBuilder>();
        A.CallTo(() => localTree.BuildTreeAsync(A<TreeNodeType>._, A<CancellationToken>._))
            .Returns([new DatabaseTreeNode("My Clips", "db_local")]);

        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());

        _viewModel = new CollectionTreeViewModel(_collectionService, A.Fake<IFolderService>(), A.Fake<IClipService>(), configurationService,
            _messenger, localTree, NullLogger<CollectionTreeViewModel>.Instance, new SearchResultsCache(), A.Fake<IDatabaseIdService>(),
            remoteDatabaseService, _remoteTreeBuilder);
    }

    [After(Test)]
    public void Cleanup() => _viewModel.Dispose();

    [Test]
    public async Task RefreshRemoteDatabasesAsync_OnlineRemoteDatabase_AddsItAfterTheLocalDatabasesWithItsTree()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        var inbox = new RemoteCollectionTreeNode(new SharedCollection(Guid.NewGuid(), null, "Inbox", SharedCollectionKind.Collection, 10));
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._)).Returns([inbox]);
        await _viewModel.LoadAsync();

        // Act
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Assert
        await Assert.That(_viewModel.RootNodes.Count).IsEqualTo(2);
        await Assert.That(_viewModel.RootNodes[0]).IsTypeOf<DatabaseTreeNode>();
        var remote = (RemoteDatabaseTreeNode)_viewModel.RootNodes[1];
        await Assert.That(remote.Name).IsEqualTo("Work — LAPTOP");
        await Assert.That(remote.IsUnavailable).IsFalse();
        await Assert.That(remote.Children.Single()).IsSameReferenceAs(inbox);
    }

    [Test]
    public async Task RefreshRemoteDatabasesAsync_PeerOffline_ShowsTheNodeGreyedOutWithLastSeenWithoutFetchingItsTree()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Offline, DateTime.Now.AddHours(-2)));

        // Act
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Assert
        var remote = (RemoteDatabaseTreeNode)_viewModel.RootNodes.Single();
        await Assert.That(remote.IsUnavailable).IsTrue();
        await Assert.That(remote.Name).IsEqualTo("Work — LAPTOP (offline · last seen 2 h ago)");
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task RefreshRemoteDatabasesAsync_PeerGoesOffline_GreysOutTheNodeAndItsCollections()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        var inbox = new RemoteCollectionTreeNode(new SharedCollection(Guid.NewGuid(), null, "Inbox", SharedCollectionKind.Collection, 10));
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._)).Returns([inbox]);
        await _viewModel.RefreshRemoteDatabasesAsync();
        _remoteDatabases[0] = Remote(PeerConnectionStatus.Offline, DateTime.Now);

        // Act
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Assert
        var remote = (RemoteDatabaseTreeNode)_viewModel.RootNodes.Single();
        await Assert.That(remote.IsUnavailable).IsTrue();
        await Assert.That(inbox.IsUnavailable).IsTrue();
        await Assert.That(remote.Children).Contains(inbox);
    }

    [Test]
    public async Task RefreshRemoteDatabasesAsync_DatabaseUnsharedOrPeerUnpaired_RemovesTheNode()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        await _viewModel.RefreshRemoteDatabasesAsync();
        _viewModel.SelectedNode = _viewModel.RootNodes.Single();
        _remoteDatabases.Clear();

        // Act
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Assert
        await Assert.That(_viewModel.RootNodes).IsEmpty();
        await Assert.That(_viewModel.SelectedNode).IsNull();
    }

    [Test]
    public async Task RefreshRemoteDatabasesAsync_PeerUnreachableWhileLoading_KeepsTheNodeAndRetriesLater()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._))
            .Throws(new RemoteDatabaseUnavailableException("LAPTOP cannot be reached."));

        // Act
        await _viewModel.RefreshRemoteDatabasesAsync();
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Assert
        await Assert.That(_viewModel.RootNodes.Single()).IsTypeOf<RemoteDatabaseTreeNode>();
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Test]
    public async Task SelectedNode_RemoteCollection_SendsRemoteSelectionAndNeverChangesTheCaptureTarget()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        var inbox = new SharedCollection(Guid.NewGuid(), null, "Inbox", SharedCollectionKind.Collection, 10);
        var projects = new SharedCollection(Guid.NewGuid(), inbox.Id, "Projects", SharedCollectionKind.Folder, 5);
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._))
            .ReturnsLazily(call => Task.FromResult(RemoteDatabaseTreeBuilder.BuildChildren(call.GetArgument<RemoteDatabaseTreeNode>(0)!, [inbox, projects], false)));
        await _viewModel.RefreshRemoteDatabasesAsync();
        var remote = _viewModel.RootNodes.Single();
        var folderNode = remote.Children.Single().Children.Single();

        var remoteSelections = new List<RemoteDatabaseNodeSelectedEvent>();
        var localSelections = new List<CollectionNodeSelectedEvent>();
        _messenger.Register<RemoteDatabaseNodeSelectedEvent>(this, (_, m) => remoteSelections.Add(m));
        _messenger.Register<CollectionNodeSelectedEvent>(this, (_, m) => localSelections.Add(m));

        // Act
        _viewModel.SelectedNode = remote.Children.Single();
        _viewModel.SelectedNode = folderNode;
        _viewModel.SelectedNode = remote;

        // Assert
        await Assert.That(remoteSelections).IsEquivalentTo([
            new RemoteDatabaseNodeSelectedEvent(_work, inbox.Id, null),
            new RemoteDatabaseNodeSelectedEvent(_work, inbox.Id, projects.Id),
            new RemoteDatabaseNodeSelectedEvent(_work, null, null),
        ]);
        await Assert.That(localSelections).IsEmpty();
        A.CallTo(() => _collectionService.SetActiveAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task RemoteNodes_AreNeitherDatabaseNorCollectionNodes_SoNoLocalDatabaseOrCollectionActionAppliesToThem()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        var inbox = new SharedCollection(Guid.NewGuid(), null, "Inbox", SharedCollectionKind.Collection, 10);
        var today = new SharedCollection(Guid.NewGuid(), null, "Today", SharedCollectionKind.Smart, 20);
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._))
            .ReturnsLazily(call => Task.FromResult(RemoteDatabaseTreeBuilder.BuildChildren(call.GetArgument<RemoteDatabaseTreeNode>(0)!, [inbox, today], false)));

        // Act
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Assert
        var all = Flatten(_viewModel.RootNodes).ToList();
        await Assert.That(all.OfType<DatabaseTreeNode>()).IsEmpty();
        await Assert.That(all.OfType<CollectionTreeNode>()).IsEmpty();
        await Assert.That(all.OfType<VirtualCollectionTreeNode>()).IsEmpty();
        await Assert.That(all.OfType<FolderTreeNode>()).IsEmpty();
        _viewModel.SelectedNode = all.OfType<RemoteCollectionTreeNode>().First();
        await Assert.That(_viewModel.MoveUpCommand.CanExecute(null)).IsFalse();
        await Assert.That(_viewModel.ShowAllClipsInChildrenCommand.CanExecute(null)).IsFalse();
    }

    private static RemoteDatabase Remote(PeerConnectionStatus status, DateTime? lastSeen = null) =>
        new(_work, "Work", "LAPTOP", status, lastSeen);

    private static IEnumerable<TreeNodeBase> Flatten(IEnumerable<TreeNodeBase> nodes) =>
        nodes.SelectMany(p => new[] { p }.Concat(Flatten(p.Children)));
}
