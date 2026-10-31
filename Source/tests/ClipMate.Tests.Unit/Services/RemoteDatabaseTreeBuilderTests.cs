using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Tests.Unit.Services;

public class RemoteDatabaseTreeBuilderTests
{
    private static readonly RemoteDatabaseTreeNode _database = new(
        new RemoteDatabase(new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([1]), Guid.NewGuid()), "Work", "LAPTOP",
            PeerConnectionStatus.Online, null), DateTime.Now);

    [Test]
    public async Task BuildChildren_LaysOutCollectionsWithNestedFoldersThenSmartAndSqlBasedCollectionsUnderVirtual()
    {
        // Arrange
        var inbox = Entry("Inbox", CollectionEntryKind.Collection, 20);
        var safe = Entry("Safe", CollectionEntryKind.Collection, 10);
        var projects = Entry("Projects", CollectionEntryKind.Folder, 1, inbox.Id);
        var archive = Entry("Archive", CollectionEntryKind.Folder, 1, projects.Id);
        var today = Entry("Today", CollectionEntryKind.Smart, 30);
        var bitmaps = Entry("Bitmaps", CollectionEntryKind.SqlBased, 31);

        // Act
        var children = RemoteDatabaseTreeBuilder.BuildChildren(_database, [today, inbox, archive, safe, bitmaps, projects], false);

        // Assert
        await Assert.That(children.Select(p => p.Name)).IsEquivalentTo(["Safe", "Inbox", "Virtual"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var inboxNode = children[1];
        await Assert.That(inboxNode.Children.Single().Name).IsEqualTo("Projects");
        await Assert.That(inboxNode.Children.Single().Children.Single().Name).IsEqualTo("Archive");
        await Assert.That(inboxNode.Children.Single()).IsTypeOf<RemoteFolderTreeNode>();
        await Assert.That(children[2].Children.Select(p => p.Name)).IsEquivalentTo(["Today", "Bitmaps"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(children[2].Children.All(p => p.NodeType == TreeNodeType.VirtualCollection)).IsTrue();
    }

    [Test]
    public async Task BuildChildren_SortAlphabetically_OrdersCollectionsByTitle()
    {
        // Act
        var children = RemoteDatabaseTreeBuilder.BuildChildren(_database,
            [Entry("Safe", CollectionEntryKind.Collection, 1), Entry("Inbox", CollectionEntryKind.Collection, 2)], true);

        // Assert
        await Assert.That(children.Select(p => p.Name)).IsEquivalentTo(["Inbox", "Safe"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task BuildChildren_FolderWhoseParentIsMissing_IsShownAsACollection()
    {
        // Arrange
        var orphan = Entry("Orphan", CollectionEntryKind.Folder, 1, Guid.NewGuid());
        var inbox = Entry("Inbox", CollectionEntryKind.Collection, 2);
        var loopA = new CollectionEntry(Guid.NewGuid(), inbox.Id, "A", CollectionEntryKind.Folder, 1);

        // Act
        var children = RemoteDatabaseTreeBuilder.BuildChildren(_database, [orphan, inbox, loopA], false);

        // Assert
        await Assert.That(children.Select(p => p.Name)).IsEquivalentTo(["Orphan", "Inbox"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(children[1].Children.Single().Name).IsEqualTo("A");
    }

    private static CollectionEntry Entry(string title, CollectionEntryKind kind, int sortKey, Guid? parentId = null) =>
        new(Guid.NewGuid(), parentId, title, kind, sortKey);
}
