using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using ClipMate.Tests.Unit.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The Search Results node of Remote Databases, and what the search dialog searches given the tree selection.
/// </summary>
public class CollectionTreeViewModelRemoteSearchTests
{
    private static readonly RemoteDatabaseAddress _work = new(DeviceId.FromCertificateBytes([1]), Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly CollectionEntry _inbox = new(Guid.NewGuid(), null, "Inbox", CollectionEntryKind.Collection, 10);
    private static readonly CollectionEntry _projects = new(Guid.NewGuid(), _inbox.Id, "Projects", CollectionEntryKind.Folder, 5);
    private static readonly CollectionEntry _today = new(Guid.NewGuid(), null, "Today", CollectionEntryKind.Smart, 20);
    private RemoteSearchResultsCache _cache = null!;
    private ICollectionTreeBuilder _localTree = null!;
    private StrongReferenceMessenger _messenger = null!;
    private FakeDatabases _databases = null!;
    private CollectionTreeViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _messenger = new StrongReferenceMessenger();
        _cache = new RemoteSearchResultsCache();
        _databases = new FakeDatabases();
        _databases.AddRemote(new RemoteDatabase(_work, "Work", "LAPTOP", PeerConnectionStatus.Online, null)).Tree = [_inbox, _projects, _today];
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());

        // The local tree is set by each test; a Remote Database's is laid out by the real builder
        var builder = new CollectionTreeBuilder(configurationService, _databases, A.Fake<ICollectionService>(), NullLogger<CollectionTreeBuilder>.Instance);
        _localTree = A.Fake<ICollectionTreeBuilder>();
        A.CallTo(() => _localTree.BuildTreeAsync(A<TreeNodeType>._, A<CancellationToken>._)).Returns([]);
        A.CallTo(() => _localTree.BuildChildrenAsync(A<IDatabase>._, A<TreeNodeBase>._, A<TreeNodeType>._, A<CancellationToken>._))
            .ReturnsLazily(call => builder.BuildChildrenAsync(call.GetArgument<IDatabase>(0)!, call.GetArgument<TreeNodeBase>(1)!));

        _viewModel = new CollectionTreeViewModel(A.Fake<ICollectionService>(), A.Fake<IFolderService>(), A.Fake<IClipService>(), configurationService,
            _messenger, _localTree, NullLogger<CollectionTreeViewModel>.Instance, new SearchResultsCache(), A.Fake<IDatabaseIdService>(),
            A.Fake<IRemoteDatabaseService>(), _databases, A.Fake<ISharedDatabaseService>(), A.Fake<IPeerDialogService>(), _cache);
    }

    [After(Test)]
    public void Cleanup() => _viewModel.Dispose();

    private RemoteDatabaseTreeNode Remote => _viewModel.RootNodes.OfType<RemoteDatabaseTreeNode>().Single();

    private RemoteSearchResultsTreeNode SearchNode => Remote.Children.OfType<RemoteSearchResultsTreeNode>().Single();

    [Test]
    public async Task RefreshRemoteDatabasesAsync_LoadedRemoteDatabase_EndsWithASearchResultsNode()
    {
        // Act
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Assert
        await Assert.That(Remote.Children.Last()).IsTypeOf<RemoteSearchResultsTreeNode>();
        await Assert.That(SearchNode.Name).IsEqualTo("Search Results");
        await Assert.That(SearchNode.Address).IsEqualTo(_work);
    }

    [Test]
    public async Task RefreshRemoteDatabasesAsync_EarlierRemoteSearch_ShowsTheCachedResults()
    {
        // Arrange
        _cache.SetResults(_work, new RemoteSearchResult("report", Clips(1000), 4212));

        // Act
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Assert
        await Assert.That(SearchNode.Name).IsEqualTo("Search: \"report\" (showing 1,000 of 4,212)");
    }

    [Test]
    public async Task Receive_RemoteSearchExecuted_ShowsTheResultsAndSelectsTheSearchResultsNode()
    {
        // Arrange
        await _viewModel.RefreshRemoteDatabasesAsync();
        var result = new RemoteSearchResult("report", Clips(2), 2);
        _cache.SetResults(_work, result);
        var shown = new List<RemoteSearchResultsSelectedEvent>();
        _messenger.Register<RemoteSearchResultsSelectedEvent>(this, (_, m) => shown.Add(m));

        // Act
        _viewModel.Receive(new SearchExecutedEvent(RemoteDatabaseKey.From(_work), "report", result.Clips.Select(p => p.Id).ToList()));

        // Assert
        await Assert.That(_viewModel.SelectedNode).IsSameReferenceAs(SearchNode);
        await Assert.That(SearchNode.Name).IsEqualTo("Search: \"report\" (2 results)");
        await Assert.That(shown.Single().Address).IsEqualTo(_work);
        await Assert.That(shown.Single().Result).IsSameReferenceAs(result);
    }

    [Test]
    public async Task Receive_RemoteSearchExecutedThatDoesNotSelect_ShowsTheResultsAndKeepsTheSelection()
    {
        // Arrange: an "All Databases" search fills each node, then selects one at the end
        await _viewModel.RefreshRemoteDatabasesAsync();
        var inbox = Remote.Children.OfType<RemoteCollectionTreeNode>().Single();
        _viewModel.SelectedNode = inbox;
        var result = new RemoteSearchResult("report", Clips(2), 2);
        _cache.SetResults(_work, result);

        // Act
        _viewModel.Receive(new SearchExecutedEvent(RemoteDatabaseKey.From(_work), "report", result.Clips.Select(p => p.Id).ToList()) { SelectsNode = false });

        // Assert
        await Assert.That(_viewModel.SelectedNode).IsSameReferenceAs(inbox);
        await Assert.That(SearchNode.Name).IsEqualTo("Search: \"report\" (2 results)");
    }

    [Test]
    public async Task Receive_RemoteSearchExecutedWhileItsNodeIsSelected_ShowsTheNewResults()
    {
        // Arrange
        await _viewModel.RefreshRemoteDatabasesAsync();
        _cache.SetResults(_work, new RemoteSearchResult("report", Clips(2), 2));
        _viewModel.SelectedNode = SearchNode;
        var latest = new RemoteSearchResult("memo", Clips(1), 1);
        _cache.SetResults(_work, latest);
        var shown = new List<RemoteSearchResultsSelectedEvent>();
        _messenger.Register<RemoteSearchResultsSelectedEvent>(this, (_, m) => shown.Add(m));

        // Act
        _viewModel.Receive(new SearchExecutedEvent(RemoteDatabaseKey.From(_work), "memo", latest.Clips.Select(p => p.Id).ToList()));

        // Assert
        await Assert.That(shown.Single().Result).IsSameReferenceAs(latest);
    }

    [Test]
    public async Task SelectedNode_RemoteSearchResultsBeforeAnySearch_OpensTheSearchDialog()
    {
        // Arrange
        await _viewModel.RefreshRemoteDatabasesAsync();
        var opened = 0;
        _messenger.Register<ShowSearchWindowEvent>(this, (_, _) => opened++);

        // Act
        _viewModel.SelectedNode = SearchNode;

        // Assert
        await Assert.That(opened).IsEqualTo(1);
    }

    [Test]
    public async Task RefreshRemoteDatabasesAsync_RemoteDatabaseGone_ForgetsItsSearchResults()
    {
        // Arrange: unshared, or its Peer was unpaired
        await _viewModel.RefreshRemoteDatabasesAsync();
        _cache.SetResults(_work, new RemoteSearchResult("report", Clips(1), 1));
        _databases.Remove(RemoteDatabaseKey.From(_work));

        // Act
        await _viewModel.RefreshRemoteDatabasesAsync();

        // Assert
        await Assert.That(_cache.GetResults(_work)).IsNull();
    }

    [Test]
    public async Task GetSearchTarget_RemoteFolder_IsTheRemoteDatabaseWithItsCollectionAndFolder()
    {
        // Arrange
        await _viewModel.RefreshRemoteDatabasesAsync();
        _viewModel.SelectedNode = Remote.Children.OfType<RemoteCollectionTreeNode>().Single().Children.Single();

        // Act
        var target = _viewModel.GetSearchTarget();

        // Assert
        await Assert.That(target).IsNotNull();
        await Assert.That(target!.DatabaseKey).IsEqualTo(RemoteDatabaseKey.From(_work));
        await Assert.That(target.IsRemote).IsTrue();
        await Assert.That(target.DatabaseName).IsEqualTo("Work — LAPTOP");
        await Assert.That(target.Collection?.Id).IsEqualTo(_inbox.Id);
        await Assert.That(target.Collection?.Name).IsEqualTo("Inbox");
        await Assert.That(target.Folder?.Id).IsEqualTo(_projects.Id);
        await Assert.That(target.Folder?.Name).IsEqualTo("Projects");
    }

    [Test]
    public async Task GetSearchTarget_RemoteSmartCollection_OffersNoCollectionScope()
    {
        // Arrange: a Smart collection has no clips of its own to search within
        await _viewModel.RefreshRemoteDatabasesAsync();
        _viewModel.SelectedNode = Remote.Children.OfType<VirtualCollectionsContainerNode>().Single().Children.Single();

        // Act
        var target = _viewModel.GetSearchTarget();

        // Assert
        await Assert.That(target!.IsRemote).IsTrue();
        await Assert.That(target.Collection?.Id).IsNull();
        await Assert.That(target.Folder?.Id).IsNull();
    }

    [Test]
    public async Task GetSearchTarget_LocalFolder_IsTheLocalDatabaseWithItsCollectionAndFolder()
    {
        // Arrange
        var database = new DatabaseTreeNode("My Clips", "db_local");
        var inbox = new CollectionTreeNode(new Collection { Id = Guid.NewGuid(), Title = "Inbox" }) { Parent = database };
        var folder = new FolderTreeNode(new Folder { Id = Guid.NewGuid(), CollectionId = inbox.Collection.Id, Name = "Projects" }) { Parent = inbox };
        database.Children.Add(inbox);
        inbox.Children.Add(folder);
        A.CallTo(() => _localTree.BuildTreeAsync(A<TreeNodeType>._, A<CancellationToken>._)).Returns([database]);
        await _viewModel.LoadAsync();
        _viewModel.SelectedNode = folder;

        // Act
        var target = _viewModel.GetSearchTarget();

        // Assert
        await Assert.That(target!.DatabaseKey).IsEqualTo("db_local");
        await Assert.That(target.IsRemote).IsFalse();
        await Assert.That(target.DatabaseName).IsEqualTo("My Clips");
        await Assert.That(target.Collection?.Id).IsEqualTo(inbox.Collection.Id);
        await Assert.That(target.Folder?.Id).IsEqualTo(folder.Folder.Id);
        await Assert.That(target.Folder?.Name).IsEqualTo("Projects");
    }

    [Test]
    public async Task GetSearchTarget_NothingSelected_IsNull()
    {
        // Assert
        await Assert.That(_viewModel.GetSearchTarget()).IsNull();
    }

    private static List<Clip> Clips(int count) => Enumerable.Range(0, count).Select(_ => new Clip { Id = Guid.NewGuid() }).ToList();
}
