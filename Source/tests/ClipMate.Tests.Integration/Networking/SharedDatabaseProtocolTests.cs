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
            "SharedDatabaseAccess.GetClipPage",
            "SharedDatabaseAccess.GetClipFormats",
            "SharedDatabaseAccess.ReadClipContent",
            "SharedDatabaseAccess.WatchChanges",
        ]);
    }

    [Test]
    public async Task PeerProtocol_NoRequestCarriesSql()
    {
        // ADR 0001: SQL never crosses the wire; Smart and SQL-based collections run the Peer's own stored SQL.
        // Act
        var requestFields = SharedDatabaseAccess.Descriptor.Methods
            .SelectMany(p => p.InputType.Fields.InDeclarationOrder())
            .Select(p => p.Name)
            .ToList();

        // Assert
        await Assert.That(requestFields.Any(p => p.Contains("sql", StringComparison.OrdinalIgnoreCase) ||
                                                 p.Contains("query", StringComparison.OrdinalIgnoreCase))).IsFalse();
    }

    [Test]
    public async Task PeerProtocol_AdvertisesRemoteClips()
    {
        // Assert
        await Assert.That(PeerProtocol.Capabilities).Contains(PeerProtocol.RemoteClipsCapability);
    }

    [Test]
    public async Task FromMessage_ClipPage_DropsClipsWithInvalidIdsAndCleansText()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reply = new GetClipPageReply { Offset = 0, TotalCount = 2 };
        reply.Clips.Add(new ClipSummary { Id = id.ToString(), Title = "Hello\u0000 world\u001b", SourceApplicationName = new string('x', 5000), Size = -5 });
        reply.Clips.Add(new ClipSummary { Id = "not a guid", Title = "Broken" });

        // Act
        var page = SharedDatabaseMessages.FromMessage(reply);

        // Assert
        var clip = page.Clips.Single();
        await Assert.That(clip.Id).IsEqualTo(id);
        await Assert.That(clip.Title).IsEqualTo("Hello world");
        await Assert.That(clip.SourceApplicationName!.Length).IsEqualTo(2048);
        await Assert.That(clip.Size).IsEqualTo(0);
    }

    [Test]
    public async Task PeerProtocol_AdvertisesChangeNotifications()
    {
        // Assert
        await Assert.That(PeerProtocol.Capabilities).Contains(PeerProtocol.ChangeNotificationsCapability);
    }

    [Test]
    public async Task ChangeNotice_CarriesOnlyIds_NeverClipData()
    {
        // Act: every field reachable from a change notice
        var fields = new List<string>();
        var pending = new Queue<Google.Protobuf.Reflection.MessageDescriptor>([ChangeNotice.Descriptor]);
        while (pending.TryDequeue(out var message))
        {
            foreach (var item in message.Fields.InDeclarationOrder())
            {
                if (item.FieldType == Google.Protobuf.Reflection.FieldType.Message)
                    pending.Enqueue(item.MessageType);
                else
                    fields.Add(item.Name);
            }
        }

        // Assert
        await Assert.That(fields).IsEquivalentTo(["database_id", "collection_id"]);
    }

    [Test]
    public async Task FromMessage_ChangeNotice_DropsNoticesWithInvalidIds()
    {
        // Arrange
        var databaseId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();

        // Act
        var valid = SharedDatabaseMessages.FromMessage(new ChangeNotice
        {
            CollectionChanged = new CollectionChanged { DatabaseId = databaseId.ToString(), CollectionId = collectionId.ToString() },
        });
        var invalid = SharedDatabaseMessages.FromMessage(new ChangeNotice
        {
            CollectionChanged = new CollectionChanged { DatabaseId = "not a guid", CollectionId = collectionId.ToString() },
        });
        var shareList = SharedDatabaseMessages.FromMessage(new ChangeNotice { SharedDatabasesChanged = new SharedDatabasesChanged() });
        var empty = SharedDatabaseMessages.FromMessage(new ChangeNotice());

        // Assert
        await Assert.That(valid).IsEqualTo(new CollectionChangedNotice(new SharedCollectionChange(databaseId, collectionId)));
        await Assert.That(invalid).IsNull();
        await Assert.That(shareList).IsTypeOf<SharedDatabasesChangedNotice>();
        await Assert.That(empty).IsNull();
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
