using ClipMate.Core.Models.Networking;
using ClipMate.Networking;
using ClipMate.Networking.Protocol;
using ClipMate.Networking.SharedDatabases;

namespace ClipMate.Tests.Integration.Networking;

/// <summary>
/// The Shared Database wire protocol. Lives here rather than in the unit tests because it loads the gRPC assemblies,
/// which the unit tests' NetworkingStartupTests requires to stay unloaded.
/// </summary>
public class SharedDatabaseProtocolTests
{
    [Test]
    public async Task PeerProtocol_ServesOnlyHandshakePairingAndSharedDatabaseReads()
    {
        // Templates, shortcuts, application profiles, users, settings, editor state and SQL must never be served:
        // a new call has to be added here deliberately.
        // Act
        var methods = PeerService.Descriptor.Methods.Select(p => $"{p.Service.Name}.{p.Name}")
            .Concat(SharedDatabaseAccess.Descriptor.Methods.Select(p => $"{p.Service.Name}.{p.Name}"));

        // Assert
        await Assert.That(methods).IsEquivalentTo([
            "PeerService.Hello",
            "PeerService.RequestPairing",
            "PeerService.ConfirmPairing",
            "PeerService.Unpair",
            "PeerService.CheckPairing",
            "SharedDatabaseAccess.ListSharedDatabases",
            "SharedDatabaseAccess.GetCollectionTree",
        ]);
    }

    [Test]
    public async Task PeerProtocol_AdvertisesSharedDatabases()
    {
        // Assert
        await Assert.That(PeerProtocol.Capabilities).Contains(PeerProtocol.SharedDatabasesCapability);
    }

    [Test]
    public async Task FromMessage_SharedDatabaseList_DropsInvalidAndRepeatedIdsAndCleansNames()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reply = new ListSharedDatabasesReply();
        reply.Databases.Add(new SharedDatabaseInfo { DatabaseId = id.ToString(), Name = " Work\u0007 " });
        reply.Databases.Add(new SharedDatabaseInfo { DatabaseId = id.ToString(), Name = "Work again" });
        reply.Databases.Add(new SharedDatabaseInfo { DatabaseId = "not a guid", Name = "Broken" });
        reply.Databases.Add(new SharedDatabaseInfo { DatabaseId = Guid.Empty.ToString(), Name = new string('x', 500) });

        // Act
        var databases = SharedDatabaseMessages.FromMessage(reply);

        // Assert
        await Assert.That(databases.Count).IsEqualTo(2);
        await Assert.That(databases[0]).IsEqualTo(new SharedDatabase(id, "Work"));
        await Assert.That(databases[1].Name.Length).IsEqualTo(128);
    }

    [Test]
    public async Task FromMessage_CollectionTree_RoundTripsKindsAndParents()
    {
        // Arrange
        var inbox = new SharedCollection(Guid.NewGuid(), null, "Inbox", SharedCollectionKind.Collection, 10);
        var tree = new[]
        {
            inbox,
            new SharedCollection(Guid.NewGuid(), inbox.Id, "Projects", SharedCollectionKind.Folder, 5),
            new SharedCollection(Guid.NewGuid(), null, "Today", SharedCollectionKind.Smart, 30),
            new SharedCollection(Guid.NewGuid(), null, "Bitmaps", SharedCollectionKind.SqlBased, -1),
        };
        var reply = new GetCollectionTreeReply();
        reply.Collections.AddRange(tree.Select(SharedDatabaseMessages.ToMessage));

        // Act
        var result = SharedDatabaseMessages.FromMessage(reply);

        // Assert
        await Assert.That(result).IsEquivalentTo(tree);
    }
}
