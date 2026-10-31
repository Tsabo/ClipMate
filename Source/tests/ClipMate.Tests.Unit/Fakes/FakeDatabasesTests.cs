using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Tests.Unit.Fakes;

/// <summary>
/// The behavior of <see cref="FakeDatabases" /> that App tests rely on, kept in line with the real Databases.
/// </summary>
public class FakeDatabasesTests
{
    private static readonly RemoteDatabaseAddress _address = new(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());

    [Test]
    public async Task AddRemote_IsReadOnlyAndAvailableWhileOnline()
    {
        // Arrange
        var databases = new FakeDatabases();

        // Act
        var online = databases.AddRemote(new RemoteDatabase(_address, "Clips", "LAPTOP", PeerConnectionStatus.Online, null));
        var offline = databases.AddRemote(new RemoteDatabase(new RemoteDatabaseAddress(_address.DeviceId, Guid.NewGuid()), "Notes", "LAPTOP",
            PeerConnectionStatus.Offline, null));

        // Assert
        await Assert.That(online.IsReadOnly).IsTrue();
        await Assert.That(online.IsAvailable).IsTrue();
        await Assert.That(offline.IsAvailable).IsFalse();
        await Assert.That(databases.Get(RemoteDatabaseKey.From(_address))).IsSameReferenceAs(online);
    }

    [Test]
    public async Task All_LocalDatabasesComeFirst()
    {
        // Arrange
        var databases = new FakeDatabases();
        databases.AddRemote(new RemoteDatabase(_address, "Clips", "LAPTOP", PeerConnectionStatus.Online, null));
        databases.AddLocal("db_main", "My Clips");

        // Act & Assert
        await Assert.That(databases.All[0].Key).IsEqualTo("db_main");
    }

    [Test]
    public async Task Get_UnknownKey_IsUnavailable()
    {
        // Act & Assert
        await Assert.That(() => new FakeDatabases().Get("db_missing")).Throws<DatabaseUnavailableException>();
    }

    [Test]
    public async Task GetClipPageAsync_PagesTheClipsOfThatScope()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var database = new FakeDatabases().AddLocal("db_main", "My Clips")
            .WithClips(new ClipListScope.Collection(collectionId), Clip("C"), Clip("B"), Clip("A"));

        // Act
        var page = await database.GetClipPageAsync(new ClipListScope.Collection(collectionId), 1, 1);
        var trash = await database.GetClipPageAsync(new ClipListScope.Trash(), 0, 10);

        // Assert
        await Assert.That(page.Clips.Single().Title).IsEqualTo("B");
        await Assert.That(page.TotalCount).IsEqualTo(3);
        await Assert.That(trash.Clips).IsEmpty();
    }

    [Test]
    public async Task Reads_Unavailable_Throw()
    {
        // Arrange
        var database = new FakeDatabases().AddLocal("db_main", "My Clips");
        database.IsAvailable = false;

        // Act & Assert
        await Assert.That(async () => await database.GetTreeAsync()).Throws<DatabaseUnavailableException>();
        await Assert.That(async () => await database.SearchAsync(new SearchFilters())).Throws<DatabaseUnavailableException>();
    }

    [Test]
    public async Task GetClipPageAsync_RemoteTrash_IsNotSupported()
    {
        // Arrange
        var database = new FakeDatabases().AddRemote(new RemoteDatabase(_address, "Clips", "LAPTOP", PeerConnectionStatus.Online, null));

        // Act & Assert
        await Assert.That(async () => await database.GetClipPageAsync(new ClipListScope.Trash(), 0, 10)).Throws<NotSupportedException>();
    }

    private static Clip Clip(string title) => new() { Id = Guid.NewGuid(), Title = title };
}
