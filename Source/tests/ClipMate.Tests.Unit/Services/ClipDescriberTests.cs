using ClipMate.App.Services;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public class ClipDescriberTests
{
    private readonly ICollectionService _collections = A.Fake<ICollectionService>();
    private readonly IRemoteDatabaseService _remoteDatabases = A.Fake<IRemoteDatabaseService>();

    [Test]
    public async Task DescribeAsync_LocalClip_GivesItsTitleAndCollection()
    {
        // Arrange
        var inbox = new Collection { Id = Guid.NewGuid(), Title = "Inbox" };
        A.CallTo(() => _collections.GetAllByDatabaseKeyAsync("db_local", A<CancellationToken>._))
            .Returns([new Collection { Id = Guid.NewGuid(), Title = "Safe" }, inbox]);

        var clip = new Clip { Id = Guid.NewGuid(), CollectionId = inbox.Id, Title = "Meeting notes" };

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
        A.CallTo(() => _remoteDatabases.GetRemoteDatabases())
            .Returns([new RemoteDatabase(address, "My Clips", "Work laptop", PeerConnectionStatus.Online, null)]);

        A.CallTo(() => _remoteDatabases.GetCollectionTreeAsync(address, A<CancellationToken>._))
            .Returns([new CollectionEntry(inboxId, null, "Inbox", CollectionEntryKind.Collection, 0)]);

        var clip = new Clip { Id = Guid.NewGuid(), CollectionId = inboxId, Title = "Build log" };

        // Act
        var description = await CreateDescriber().DescribeAsync(clip, RemoteDatabaseKey.From(address));

        // Assert
        await Assert.That(description.Collection).IsEqualTo("Inbox on Work laptop");
        A.CallTo(() => _collections.GetAllByDatabaseKeyAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private ClipDescriber CreateDescriber() => new(_collections, _remoteDatabases);
}
