using ClipMate.App.Services;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Tests.Unit.Fakes;

namespace ClipMate.Tests.Unit.Services;

public class ClipDescriberTests
{
    private readonly FakeDatabases _databases = new();

    [Test]
    public async Task DescribeAsync_LocalClip_GivesItsTitleAndCollection()
    {
        // Arrange
        var inboxId = Guid.NewGuid();
        _databases.AddLocal("db_local", "My Clips").Tree =
        [
            new CollectionEntry(Guid.NewGuid(), null, "Safe", CollectionEntryKind.Collection, 0),
            new CollectionEntry(inboxId, null, "Inbox", CollectionEntryKind.Collection, 1),
        ];

        var clip = new Clip { Id = Guid.NewGuid(), CollectionId = inboxId, Title = "Meeting notes" };

        // Act
        var description = await CreateDescriber().DescribeAsync(clip, "db_local");

        // Assert
        await Assert.That(description.Title).IsEqualTo("Meeting notes");
        await Assert.That(description.Collection).IsEqualTo("Inbox");
    }

    [Test]
    public async Task DescribeAsync_EncryptedClipWithItsKeyRemembered_HidesItsTitle()
    {
        // Arrange - a custom title and a remembered key would both show the title in the clip list
        _databases.AddLocal("db_local", "My Clips");
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Bank PIN", CustomTitle = true, Encrypted = true, IsDecrypted = true };

        // Act
        var description = await CreateDescriber().DescribeAsync(clip, "db_local");

        // Assert
        await Assert.That(description.Title).IsEqualTo("Encrypted clip");
    }

    [Test]
    public async Task DescribeAsync_ClipOfARemoteDatabase_NamesTheCollectionOnThePeer()
    {
        // Arrange
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([7]), Guid.NewGuid());
        var inboxId = Guid.NewGuid();
        _databases.AddRemote(new RemoteDatabase(address, "My Clips", "Work laptop", PeerConnectionStatus.Online, null)).Tree =
            [new CollectionEntry(inboxId, null, "Inbox", CollectionEntryKind.Collection, 0)];

        var clip = new Clip { Id = Guid.NewGuid(), CollectionId = inboxId, Title = "Build log" };

        // Act
        var description = await CreateDescriber().DescribeAsync(clip, RemoteDatabaseKey.From(address));

        // Assert
        await Assert.That(description.Collection).IsEqualTo("Inbox on Work laptop");
    }

    [Test]
    public async Task DescribeAsync_PeerOffline_NamesTheRemoteDatabaseOnThePeer()
    {
        // Arrange
        var address = new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([7]), Guid.NewGuid());
        _databases.AddRemote(new RemoteDatabase(address, "My Clips", "Work laptop", PeerConnectionStatus.Offline, null));
        var clip = new Clip { Id = Guid.NewGuid(), CollectionId = Guid.NewGuid(), Title = "Build log" };

        // Act
        var description = await CreateDescriber().DescribeAsync(clip, RemoteDatabaseKey.From(address));

        // Assert
        await Assert.That(description.Collection).IsEqualTo("My Clips on Work laptop");
    }

    private ClipDescriber CreateDescriber() => new(_databases);
}
