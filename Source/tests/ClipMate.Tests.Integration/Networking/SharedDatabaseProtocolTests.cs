using System.Reflection;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Data.Services;
using ClipMate.Networking;
using ClipMate.Networking.Protocol;
using ClipMate.Networking.SharedDatabases;
using ClipMate.Networking.SharedDatabases.Notice;
using Google.Protobuf.Reflection;
using CollectionEntry = ClipMate.Core.Models.CollectionEntry;

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
            "SharedDatabaseAccess.SearchClips",
        ]);
    }

    [Test]
    public async Task PeerProtocol_NoRequestCarriesSql()
    {
        // SQL never crosses the wire; Smart and SQL-based collections run the Peer's own stored SQL, and
        // Remote Search sends structured criteria that the Peer turns into parameterized SQL itself.
        // Act
        var requestFields = SharedDatabaseAccess.Descriptor.Methods
            .SelectMany(p => AllFields(p.InputType, []))
            .Select(p => p.Name)
            .ToList();

        // Assert
        await Assert.That(requestFields.Any(p => p.Contains("sql", StringComparison.OrdinalIgnoreCase) ||
                                                 p.Contains("query", StringComparison.OrdinalIgnoreCase))).IsFalse();
    }

    [Test]
    public async Task PeerProtocol_SearchRequestCarriesStructuredCriteria()
    {
        // Act
        var criteria = AllFields(SearchClipsRequest.Descriptor, []).Select(p => p.Name).ToList();

        // Assert
        await Assert.That(criteria).Contains("title_contains");
        await Assert.That(criteria).Contains("scope_id");
    }

    [Test]
    public async Task PeerProtocol_Version2_HasEverySharedDatabaseFeatureInItsBaseline()
    {
        // Assert: Shared Databases, their clips, change notices and Remote Search need no capability in version 2
        await Assert.That(PeerProtocol.Capabilities).IsEmpty();
    }

    [Test]
    public async Task SearchCriteria_RoundTripsEveryFilterRemoteSearchSupports()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        var filters = new SearchFilters
        {
            TitleQuery = "report, memo*", TextContentQuery = "'; DROP TABLE Clips; --", CreatorQuery = "jeremy", SourceUrlQuery = "example.com",
            ContentTypes = [ClipType.Text, ClipType.Files], DateRange = new DateRange(new DateTime(2026, 1, 31), new DateTime(2026, 10, 1)),
            Scope = SearchScope.SpecificFolder, FolderId = folderId, Format = "html", EncryptedOnly = true,
        };

        // Act
        var result = SharedDatabaseMessages.FromMessage(SharedDatabaseMessages.ToMessage(filters));

        // Assert
        await Assert.That(result.TitleQuery).IsEqualTo("report, memo*");
        await Assert.That(result.TextContentQuery).IsEqualTo("'; DROP TABLE Clips; --");
        await Assert.That(result.CreatorQuery).IsEqualTo("jeremy");
        await Assert.That(result.SourceUrlQuery).IsEqualTo("example.com");
        await Assert.That(result.ContentTypes!).IsEquivalentTo([ClipType.Text, ClipType.Files]);
        await Assert.That(result.DateRange).IsEqualTo(new DateRange(new DateTime(2026, 1, 31), new DateTime(2026, 10, 1)));
        await Assert.That(result.Scope).IsEqualTo(SearchScope.SpecificFolder);
        await Assert.That(result.FolderId).IsEqualTo(folderId);
        await Assert.That(result.CollectionId).IsNull();
        await Assert.That(result.Format).IsEqualTo("html");
        await Assert.That(result.EncryptedOnly == true).IsTrue();
        await Assert.That(result.IncludeDeleted).IsFalse();
        await Assert.That(result.HasShortcutOnly).IsNull();
    }

    [Test]
    public async Task SearchCriteria_EmptyCriteria_SearchesEverythingInAllCollections()
    {
        // Act
        var result = SharedDatabaseMessages.FromMessage(new SearchCriteria());

        // Assert
        await Assert.That(result.Scope).IsEqualTo(SearchScope.AllCollections);
        await Assert.That(result.TitleQuery).IsNull();
        await Assert.That(result.ContentTypes).IsNull();
        await Assert.That(result.DateRange).IsNull();
        await Assert.That(result.EncryptedOnly).IsNull();
    }

    [Test]
    public async Task SearchCriteria_FromAPeerWithUnusableValues_IsRejected()
    {
        // Assert: the Peer answers INVALID_ARGUMENT rather than guessing
        await Assert.That(() => SharedDatabaseMessages.FromMessage(new SearchCriteria { Scope = SearchScopeKind.Collection, ScopeId = "not a guid" }))
            .Throws<ArgumentException>();
        await Assert.That(() => SharedDatabaseMessages.FromMessage(new SearchCriteria { CapturedAfter = "yesterday" })).Throws<ArgumentException>();
        await Assert.That(() => SharedDatabaseMessages.FromMessage(new SearchCriteria { TitleContains = new string('x', 5000) }))
            .Throws<ArgumentException>();
        await Assert.That(() => SharedDatabaseMessages.FromMessage(new SearchCriteria { Scope = (SearchScopeKind)42 })).Throws<ArgumentException>();
        await Assert.That(() => SharedDatabaseMessages.FromMessage(new SearchCriteria { ContentKinds = { (ClipKind)42 } })).Throws<ArgumentException>();
    }

    [Test]
    public async Task PeerSearch_KeepsNoStateBetweenRequests()
    {
        // The services that run a Remote Search hold only their collaborators: nothing about a search outlives it.
        // Act
        var fields = typeof(SharedDatabaseGrpcService).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Concat(typeof(SharedDatabaseService).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        // Assert
        await Assert.That(fields.Where(p => !p.IsInitOnly).Select(p => p.Name)).IsEmpty();
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

    private static IEnumerable<FieldDescriptor> AllFields(MessageDescriptor message, HashSet<string> visited)
    {
        if (!visited.Add(message.FullName))
            yield break;

        foreach (var item in message.Fields.InDeclarationOrder())
        {
            yield return item;
            if (item.FieldType == FieldType.Message)
            {
                foreach (var nested in AllFields(item.MessageType, visited))
                    yield return nested;
            }
        }
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
        var inbox = new CollectionEntry(Guid.NewGuid(), null, "Inbox", CollectionEntryKind.Collection, 10);
        var tree = new[]
        {
            inbox,
            new CollectionEntry(Guid.NewGuid(), inbox.Id, "Projects", CollectionEntryKind.Folder, 5),
            new CollectionEntry(Guid.NewGuid(), null, "Today", CollectionEntryKind.Smart, 30),
            new CollectionEntry(Guid.NewGuid(), null, "Bitmaps", CollectionEntryKind.SqlBased, -1),
        };
        var reply = new GetCollectionTreeReply();
        reply.Collections.AddRange(tree.Select(SharedDatabaseMessages.ToMessage));

        // Act
        var result = SharedDatabaseMessages.FromMessage(reply);

        // Assert
        await Assert.That(result).IsEquivalentTo(tree);
    }
}
