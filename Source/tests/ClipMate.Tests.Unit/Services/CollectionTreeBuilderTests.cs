using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Tests.Unit.Fakes;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// The collection tree's nodes, laid out the same way for local and Remote Databases.
/// </summary>
public class CollectionTreeBuilderTests
{
    private static readonly RemoteDatabaseAddress _address = new(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
    private readonly ICollectionService _collectionService = A.Fake<ICollectionService>();
    private readonly ClipMateConfiguration _configuration = new() { DefaultDatabase = "db" };
    private readonly FakeDatabases _databases = new();
    private CollectionTreeBuilder _builder = null!;

    [Before(Test)]
    public void Setup()
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);
        _builder = new CollectionTreeBuilder(configurationService, _databases, _collectionService, NullLogger<CollectionTreeBuilder>.Instance);
    }

    [Test]
    public async Task BuildTreeAsync_FolderInACollection_IsShownOnlyUnderThatCollection()
    {
        // Arrange: as stored, a folder is a collection row with the Folder type and a parent, and the collection list
        // includes it
        var inbox = new Collection { Id = Guid.NewGuid(), Title = "Inbox", SortKey = 100, AcceptNewClips = true };
        var safe = new Collection { Id = Guid.NewGuid(), Title = "Safe", SortKey = 200, LmType = CollectionLmType.Folder };
        var work = new Collection { Id = Guid.NewGuid(), Title = "Work", SortKey = 10, LmType = CollectionLmType.Folder, ParentId = inbox.Id };
        Local([inbox, safe, work]);

        // Act
        var database = (await _builder.BuildTreeAsync(TreeNodeType.SpecialCollection)).Single();

        // Assert
        await Assert.That(string.Join(", ", database.Children.Select(p => p.Name))).IsEqualTo("Inbox, Safe");
        var inboxNode = database.Children.OfType<CollectionTreeNode>().Single(p => p.Name == "Inbox");
        var workNode = inboxNode.Children.OfType<FolderTreeNode>().Single();
        await Assert.That(workNode.Folder.Id).IsEqualTo(work.Id);
        await Assert.That(workNode.Folder.CollectionId).IsEqualTo(inbox.Id);
    }

    [Test]
    public async Task BuildTreeAsync_LocalNodesCarryTheirCollections()
    {
        // Arrange
        var inbox = new Collection { Id = Guid.NewGuid(), Title = "Inbox", SortKey = 1, AcceptNewClips = true };
        var today = new Collection { Id = Guid.NewGuid(), Title = "Today", SortKey = 2, ListType = CollectionListType.Smart };
        Local([inbox, today]);

        // Act
        var database = (await _builder.BuildTreeAsync(TreeNodeType.None)).Single();

        // Assert
        await Assert.That(database.Children.OfType<CollectionTreeNode>().Single().Collection).IsSameReferenceAs(inbox);
        var virtualNode = database.Children.OfType<VirtualCollectionsContainerNode>().Single().Children.OfType<VirtualCollectionTreeNode>().Single();
        await Assert.That(virtualNode.VirtualCollection).IsSameReferenceAs(today);
        await Assert.That(database.Children.Select(p => p.Name).TakeLast(2)).IsEquivalentTo(["Trashcan", "Search Results"]);
        await Assert.That(database.IsExpanded).IsTrue();
    }

    [Test]
    public async Task BuildTreeAsync_ExcludingVirtualAndSpecial_LeavesThemOut()
    {
        // Arrange
        Local([
            new Collection { Id = Guid.NewGuid(), Title = "Inbox", SortKey = 1 },
            new Collection { Id = Guid.NewGuid(), Title = "Today", SortKey = 2, ListType = CollectionListType.Smart },
        ]);

        // Act
        var database = (await _builder.BuildTreeAsync(TreeNodeType.VirtualCollection | TreeNodeType.SpecialCollection)).Single();

        // Assert
        await Assert.That(database.Children.Select(p => p.Name)).IsEquivalentTo(["Inbox"]);
    }

    [Test]
    public async Task BuildTreeAsync_DatabaseFileMissing_IsMarkedWithAnErrorAndHasNoChildren()
    {
        // Arrange
        _databases.AddLocal("db_archive", "Archive").IsAvailable = false;

        // Act
        var database = (DatabaseTreeNode)(await _builder.BuildTreeAsync(TreeNodeType.None)).Single();

        // Assert
        await Assert.That(database.HasError).IsTrue();
        await Assert.That(database.Children).IsEmpty();
    }

    [Test]
    public async Task BuildTreeAsync_LeavesRemoteDatabasesOut()
    {
        // Arrange
        Local([new Collection { Id = Guid.NewGuid(), Title = "Inbox" }]);
        _databases.AddRemote(new RemoteDatabase(_address, "Work", "LAPTOP", PeerConnectionStatus.Online, null));

        // Act
        var nodes = (await _builder.BuildTreeAsync(TreeNodeType.None)).ToList();

        // Assert
        await Assert.That(nodes.Single().Name).IsEqualTo("My Clips");
    }

    [Test]
    public async Task BuildChildrenAsync_RemoteDatabase_LaysOutCollectionsWithNestedFoldersThenSmartAndSqlBasedCollectionsUnderVirtual()
    {
        // Arrange
        var inbox = Entry("Inbox", CollectionEntryKind.Collection, 20);
        var safe = Entry("Safe", CollectionEntryKind.Collection, 10);
        var projects = Entry("Projects", CollectionEntryKind.Folder, 1, inbox.Id);
        var archive = Entry("Archive", CollectionEntryKind.Folder, 1, projects.Id);
        var today = Entry("Today", CollectionEntryKind.Smart, 30);
        var bitmaps = Entry("Bitmaps", CollectionEntryKind.SqlBased, 31);
        var (database, node) = Remote([today, inbox, archive, safe, bitmaps, projects]);

        // Act
        var children = await _builder.BuildChildrenAsync(database, node);

        // Assert
        await Assert.That(string.Join(", ", children.Select(p => p.Name))).IsEqualTo("Safe, Inbox, Virtual");
        var inboxNode = children[1];
        await Assert.That(inboxNode.Children.Single().Name).IsEqualTo("Projects");
        await Assert.That(inboxNode.Children.Single().Children.Single().Name).IsEqualTo("Archive");
        await Assert.That(inboxNode.Children.Single()).IsTypeOf<RemoteFolderTreeNode>();
        await Assert.That(string.Join(", ", children[2].Children.Select(p => p.Name))).IsEqualTo("Today, Bitmaps");
        await Assert.That(children[2].Children.All(p => p.NodeType == TreeNodeType.VirtualCollection)).IsTrue();
        A.CallTo(() => _collectionService.GetAllByDatabaseKeyAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task BuildChildrenAsync_SortAlphabetically_OrdersCollectionsByTitle()
    {
        // Arrange
        _configuration.Preferences.SortCollectionsAlphabetically = true;
        var (database, node) = Remote([Entry("Safe", CollectionEntryKind.Collection, 1), Entry("inbox", CollectionEntryKind.Collection, 2)]);

        // Act
        var children = await _builder.BuildChildrenAsync(database, node);

        // Assert
        await Assert.That(string.Join(", ", children.Select(p => p.Name))).IsEqualTo("inbox, Safe");
    }

    [Test]
    public async Task BuildChildrenAsync_FolderWhoseParentIsMissing_IsShownAsACollection()
    {
        // Arrange
        var orphan = Entry("Orphan", CollectionEntryKind.Folder, 1, Guid.NewGuid());
        var inbox = Entry("Inbox", CollectionEntryKind.Collection, 2);
        var folder = Entry("A", CollectionEntryKind.Folder, 1, inbox.Id);
        var (database, node) = Remote([orphan, inbox, folder]);

        // Act
        var children = await _builder.BuildChildrenAsync(database, node);

        // Assert
        await Assert.That(string.Join(", ", children.Select(p => p.Name))).IsEqualTo("Orphan, Inbox");
        await Assert.That(children[1].Children.Single().Name).IsEqualTo("A");
    }

    [Test]
    public async Task BuildChildrenAsync_FoldersInACycle_AreEachShownOnce()
    {
        // Arrange
        var inbox = Entry("Inbox", CollectionEntryKind.Collection, 1);
        var a = new CollectionEntry(Guid.NewGuid(), inbox.Id, "A", CollectionEntryKind.Folder, 1);
        var b = new CollectionEntry(Guid.NewGuid(), a.Id, "B", CollectionEntryKind.Folder, 1);
        var loop = a with { ParentId = b.Id, Title = "A again" };
        var (database, node) = Remote([inbox, a, b, loop]);

        // Act
        var children = await _builder.BuildChildrenAsync(database, node);

        // Assert
        await Assert.That(children.Single().Children.Single().Children.Single().Name).IsEqualTo("B");
    }

    [Test]
    public async Task BuildChildrenAsync_UnavailableDatabase_GreysOutEveryNode()
    {
        // Arrange
        var inbox = Entry("Inbox", CollectionEntryKind.Collection, 1);
        var (database, node) = Remote([inbox, Entry("Work", CollectionEntryKind.Folder, 1, inbox.Id), Entry("Today", CollectionEntryKind.Smart, 2)]);
        node.IsUnavailable = true;

        // Act
        var children = await _builder.BuildChildrenAsync(database, node);

        // Assert
        await Assert.That(children.All(p => p.IsUnavailable && p.Children.All(c => c.IsUnavailable))).IsTrue();
    }

    private void Local(Collection[] collections)
    {
        _databases.AddLocal("db", "My Clips").Tree = collections.Select(CollectionEntry.From).ToList();
        A.CallTo(() => _collectionService.GetAllByDatabaseKeyAsync("db", A<CancellationToken>._)).Returns(collections);
    }

    private (IDatabase Database, RemoteDatabaseTreeNode Node) Remote(CollectionEntry[] tree)
    {
        var remote = new RemoteDatabase(_address, "Work", "LAPTOP", PeerConnectionStatus.Online, null);
        var database = _databases.AddRemote(remote);
        database.Tree = [.. tree];
        return (database, new RemoteDatabaseTreeNode(remote, DateTime.Now));
    }

    private static CollectionEntry Entry(string title, CollectionEntryKind kind, int sortKey, Guid? parentId = null) =>
        new(Guid.NewGuid(), parentId, title, kind, sortKey);
}
