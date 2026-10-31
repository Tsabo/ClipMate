using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
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
    private IPeerDialogService _peerDialogs = null!;
    private IRemoteDatabaseService _remoteDatabaseService = null!;
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

        _remoteDatabaseService = remoteDatabaseService;
        _peerDialogs = A.Fake<IPeerDialogService>();

        _viewModel = new CollectionTreeViewModel(_collectionService, A.Fake<IFolderService>(), A.Fake<IClipService>(), configurationService,
            _messenger, localTree, NullLogger<CollectionTreeViewModel>.Instance, new SearchResultsCache(), A.Fake<IDatabaseIdService>(),
            remoteDatabaseService, _remoteTreeBuilder, A.Fake<ISharedDatabaseService>(), _peerDialogs);
    }

    [After(Test)]
    public void Cleanup() => _viewModel.Dispose();

    [Test]
    public async Task RefreshRemoteDatabasesAsync_OnlineRemoteDatabase_AddsItAfterTheLocalDatabasesWithItsTree()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        var inbox = new RemoteCollectionTreeNode(new CollectionEntry(Guid.NewGuid(), null, "Inbox", CollectionEntryKind.Collection, 10));
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
        await Assert.That(remote.Children.OfType<RemoteCollectionTreeNode>().Single()).IsSameReferenceAs(inbox);
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
        var inbox = new RemoteCollectionTreeNode(new CollectionEntry(Guid.NewGuid(), null, "Inbox", CollectionEntryKind.Collection, 10));
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
            .Throws(new DatabaseUnavailableException("LAPTOP cannot be reached."));

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
        var inbox = new CollectionEntry(Guid.NewGuid(), null, "Inbox", CollectionEntryKind.Collection, 10);
        var projects = new CollectionEntry(Guid.NewGuid(), inbox.Id, "Projects", CollectionEntryKind.Folder, 5);
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._))
            .ReturnsLazily(call => Task.FromResult(RemoteDatabaseTreeBuilder.BuildChildren(call.GetArgument<RemoteDatabaseTreeNode>(0)!, [inbox, projects], false)));
        await _viewModel.RefreshRemoteDatabasesAsync();
        var remote = _viewModel.RootNodes.Single();
        var folderNode = remote.Children.OfType<RemoteCollectionTreeNode>().Single().Children.Single();

        var remoteSelections = new List<RemoteDatabaseNodeSelectedEvent>();
        var localSelections = new List<CollectionNodeSelectedEvent>();
        _messenger.Register<RemoteDatabaseNodeSelectedEvent>(this, (_, m) => remoteSelections.Add(m));
        _messenger.Register<CollectionNodeSelectedEvent>(this, (_, m) => localSelections.Add(m));

        // Act
        _viewModel.SelectedNode = remote.Children.OfType<RemoteCollectionTreeNode>().Single();
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
    public async Task SelectedNode_RemoteCollectionOrFolder_SendsTheCollectionNamingThePeer()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online) with { PeerName = "Work laptop" });
        var inbox = new CollectionEntry(Guid.NewGuid(), null, "Inbox", CollectionEntryKind.Collection, 10);
        var projects = new CollectionEntry(Guid.NewGuid(), inbox.Id, "Projects", CollectionEntryKind.Folder, 5);
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._))
            .ReturnsLazily(call => Task.FromResult(RemoteDatabaseTreeBuilder.BuildChildren(call.GetArgument<RemoteDatabaseTreeNode>(0)!, [inbox, projects], false)));
        await _viewModel.RefreshRemoteDatabasesAsync();
        var remote = _viewModel.RootNodes.Single();
        var inboxNode = remote.Children.OfType<RemoteCollectionTreeNode>().Single();

        var collections = new List<string>();
        _messenger.Register<CurrentCollectionChangedEvent>(this, (_, m) => collections.Add(m.Name));

        // Act
        _viewModel.SelectedNode = inboxNode;
        _viewModel.SelectedNode = inboxNode.Children.Single();

        // Assert
        await Assert.That(collections).IsEquivalentTo(["Inbox on Work laptop", "Projects on Work laptop"]);
    }

    [Test]
    public async Task SelectedNode_LocalCollection_SendsItsName()
    {
        // Arrange
        await _viewModel.LoadAsync();
        var database = _viewModel.RootNodes.Single();
        var inbox = new CollectionTreeNode(new Collection { Id = Guid.NewGuid(), Name = "InBox" }) { Parent = database };
        database.Children.Add(inbox);

        var collections = new List<string>();
        _messenger.Register<CurrentCollectionChangedEvent>(this, (_, m) => collections.Add(m.Name));

        // Act
        _viewModel.SelectedNode = inbox;

        // Assert
        await Assert.That(collections).IsEquivalentTo(["InBox"]);
    }

    [Test]
    public async Task RemoteNodes_AreNeitherDatabaseNorCollectionNodes_SoNoLocalDatabaseOrCollectionActionAppliesToThem()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        var inbox = new CollectionEntry(Guid.NewGuid(), null, "Inbox", CollectionEntryKind.Collection, 10);
        var today = new CollectionEntry(Guid.NewGuid(), null, "Today", CollectionEntryKind.Smart, 20);
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

    [Test]
    public async Task RenameRemoteDatabaseCommand_AliasEntered_SetsTheRemoteDatabaseAlias()
    {
        // Arrange: renaming works while the Peer is offline, as the alias is local
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Offline, DateTime.Now));
        await _viewModel.RefreshRemoteDatabasesAsync();
        _viewModel.SelectedNode = _viewModel.RootNodes.Single();
        A.CallTo(() => _peerDialogs.AskRemoteDatabaseAlias(A<RemoteDatabase>.That.Matches(p => p.Address == _work))).Returns("Office clips");

        // Act
        await Assert.That(_viewModel.RenameRemoteDatabaseCommand.CanExecute(null)).IsTrue();
        await _viewModel.RenameRemoteDatabaseCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _remoteDatabaseService.SetAliasAsync(_work, "Office clips", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RenameRemoteDatabaseCommand_Blank_ClearsTheAlias()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online) with { Alias = "Office clips" });
        await _viewModel.RefreshRemoteDatabasesAsync();
        _viewModel.SelectedNode = _viewModel.RootNodes.Single();
        A.CallTo(() => _peerDialogs.AskRemoteDatabaseAlias(A<RemoteDatabase>._)).Returns("");

        // Act
        await _viewModel.RenameRemoteDatabaseCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _remoteDatabaseService.SetAliasAsync(_work, "", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RenameRemoteDatabaseCommand_Cancelled_ChangesNothing()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        await _viewModel.RefreshRemoteDatabasesAsync();
        _viewModel.SelectedNode = _viewModel.RootNodes.Single();
        A.CallTo(() => _peerDialogs.AskRemoteDatabaseAlias(A<RemoteDatabase>._)).Returns(null);

        // Act
        await _viewModel.RenameRemoteDatabaseCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _remoteDatabaseService.SetAliasAsync(A<RemoteDatabaseAddress>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task RenameRemoteDatabaseCommand_OnlyForRemoteDatabaseNodes()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        var inbox = new RemoteCollectionTreeNode(new CollectionEntry(Guid.NewGuid(), null, "Inbox", CollectionEntryKind.Collection, 10));
        A.CallTo(() => _remoteTreeBuilder.BuildChildrenAsync(A<RemoteDatabaseTreeNode>._, A<CancellationToken>._)).Returns([inbox]);
        await _viewModel.LoadAsync();
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Act / Assert
        _viewModel.SelectedNode = _viewModel.RootNodes.OfType<DatabaseTreeNode>().Single();
        await Assert.That(_viewModel.RenameRemoteDatabaseCommand.CanExecute(null)).IsFalse();
        _viewModel.SelectedNode = inbox;
        await Assert.That(_viewModel.RenameRemoteDatabaseCommand.CanExecute(null)).IsFalse();
        _viewModel.SelectedNode = null;
        await Assert.That(_viewModel.RenameRemoteDatabaseCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task RefreshRemoteDatabasesAsync_AliasSetOrCleared_RelabelsTheNode()
    {
        // Arrange
        _remoteDatabases.Add(Remote(PeerConnectionStatus.Online));
        await _viewModel.RefreshRemoteDatabasesAsync();
        var node = (RemoteDatabaseTreeNode)_viewModel.RootNodes.Single();

        // Act / Assert
        _remoteDatabases[0] = Remote(PeerConnectionStatus.Online) with { Alias = "Office clips" };
        await _viewModel.RefreshRemoteDatabasesAsync();
        await Assert.That(node.Name).IsEqualTo("Office clips — LAPTOP");

        _remoteDatabases[0] = Remote(PeerConnectionStatus.Offline, DateTime.Now.AddHours(-2)) with { Alias = "Office clips" };
        await _viewModel.RefreshRemoteDatabasesAsync();
        await Assert.That(node.Name).IsEqualTo("Office clips — LAPTOP (offline · last seen 2 h ago)");

        _remoteDatabases[0] = new RemoteDatabase(_work, "Work 2026", "LAPTOP", PeerConnectionStatus.Online, null);
        await _viewModel.RefreshRemoteDatabasesAsync();
        await Assert.That(node.Name).IsEqualTo("Work 2026 — LAPTOP");
    }

    private static RemoteDatabase Remote(PeerConnectionStatus status, DateTime? lastSeen = null) =>
        new(_work, "Work", "LAPTOP", status, lastSeen);

    private static IEnumerable<TreeNodeBase> Flatten(IEnumerable<TreeNodeBase> nodes) =>
        nodes.SelectMany(p => new[] { p }.Concat(Flatten(p.Children)));
}
