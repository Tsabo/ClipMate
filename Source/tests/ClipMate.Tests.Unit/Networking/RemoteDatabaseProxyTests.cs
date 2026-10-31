using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.SharedDatabases;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Networking;

/// <summary>
/// A Remote Database read through <see cref="IDatabase" />.
/// </summary>
public class RemoteDatabaseProxyTests
{
    private static readonly RemoteDatabaseAddress _address = new(DeviceId.FromCertificateBytes([1]), Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private IRemoteDatabaseService _remoteDatabases = null!;

    [Before(Test)]
    public void Setup() => _remoteDatabases = A.Fake<IRemoteDatabaseService>();

    [Test]
    public async Task Properties_DescribeTheRemoteDatabase()
    {
        // Arrange
        var remote = new RemoteDatabase(_address, "Shared Clips", "LAPTOP", PeerConnectionStatus.Online, null) { Alias = "Laptop clips" };

        // Act
        var database = new RemoteDatabaseProxy(remote, _remoteDatabases);

        // Assert
        await Assert.That(database.Key).IsEqualTo(RemoteDatabaseKey.From(_address));
        await Assert.That(database.Name).IsEqualTo("Laptop clips");
        await Assert.That(database.IsAvailable).IsTrue();
        await Assert.That(database.IsReadOnly).IsTrue();
        await Assert.That(database.Remote).IsSameReferenceAs(remote);
    }

    [Test]
    [Arguments(PeerConnectionStatus.Offline)]
    [Arguments(PeerConnectionStatus.UpdateRequired)]
    public async Task IsAvailable_PeerNotOnline_IsFalse(PeerConnectionStatus status)
    {
        // Act
        var database = new RemoteDatabaseProxy(new RemoteDatabase(_address, "Clips", "LAPTOP", status, null), _remoteDatabases);

        // Assert
        await Assert.That(database.IsAvailable).IsFalse();
    }

    [Test]
    public async Task GetClipPageAsync_CollectionOrFolder_AsksThePeer()
    {
        // Arrange
        var database = Online();
        var collectionId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var collectionPage = new ClipPage([], 0, 0);
        var folderPage = new ClipPage([], 0, 0);
        A.CallTo(() => _remoteDatabases.GetClipPageAsync(_address, collectionId, null, 0, 200, A<CancellationToken>._)).Returns(collectionPage);
        A.CallTo(() => _remoteDatabases.GetClipPageAsync(_address, collectionId, folderId, 0, 200, A<CancellationToken>._)).Returns(folderPage);

        // Act & Assert
        await Assert.That(await database.GetClipPageAsync(new ClipListScope.Collection(collectionId), 0, 200)).IsSameReferenceAs(collectionPage);
        await Assert.That(await database.GetClipPageAsync(new ClipListScope.Folder(collectionId, folderId), 0, 200)).IsSameReferenceAs(folderPage);
    }

    [Test]
    public async Task GetClipPageAsync_TrashOrIncludingFolders_IsNotSupported()
    {
        // Arrange
        var database = Online();

        // Act & Assert
        await Assert.That(async () => await database.GetClipPageAsync(new ClipListScope.Trash(), 0, 10)).Throws<NotSupportedException>();
        await Assert.That(async () => await database.GetClipPageAsync(new ClipListScope.Collection(Guid.NewGuid(), IncludeFolders: true), 0, 10))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task GetTreeAndContent_AskThePeer()
    {
        // Arrange
        var database = Online();
        var clipId = Guid.NewGuid();
        IReadOnlyList<CollectionEntry> tree = [new CollectionEntry(Guid.NewGuid(), null, "Inbox", CollectionEntryKind.Collection, 1)];
        A.CallTo(() => _remoteDatabases.GetCollectionTreeAsync(_address, A<CancellationToken>._)).Returns(tree);

        // Act
        var result = await database.GetTreeAsync();
        await database.GetClipContentAsync(clipId);

        // Assert
        await Assert.That(result).IsSameReferenceAs(tree);
        A.CallTo(() => _remoteDatabases.GetClipContentAsync(_address, clipId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SearchAsync_KeepsAtMostMaxResultsAndTheTotal()
    {
        // Arrange
        var database = Online();
        var filters = new SearchFilters { TitleQuery = "budget" };
        Clip[] clips = [new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() }];
        A.CallTo(() => _remoteDatabases.SearchAsync(_address, filters, A<CancellationToken>._)).Returns(new SearchMatches(clips, 3));

        // Act
        var matches = await database.SearchAsync(filters, 2);

        // Assert
        await Assert.That(matches.Clips.Count).IsEqualTo(2);
        await Assert.That(matches.TotalMatches).IsEqualTo(3);
    }

    [Test]
    public async Task Reads_PeerGoneAway_PassOnDatabaseUnavailable()
    {
        // Arrange
        var database = Online();
        A.CallTo(() => _remoteDatabases.GetCollectionTreeAsync(_address, A<CancellationToken>._))
            .ThrowsAsync(new DatabaseUnavailableException("LAPTOP is offline."));

        // Act & Assert
        await Assert.That(async () => await database.GetTreeAsync()).Throws<DatabaseUnavailableException>();
    }

    private RemoteDatabaseProxy Online() =>
        new(new RemoteDatabase(_address, "Clips", "LAPTOP", PeerConnectionStatus.Online, null), _remoteDatabases);
}
