using System.Text;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
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
/// Pins down how the whole collection tree renders, local and Remote Databases together, so reworking how the tree is
/// built (Database seam, ticket 05) can be checked against it. Every node is written out with what the tree shows for
/// it: icon, name, node type, whether it is expanded, and the styles <c>CollectionTreeControl.xaml</c> binds
/// (<c>Collection.IsReadOnly</c> maroon, <c>IsUnavailable</c> grey, <c>Collection.PurgePolicy</c> Never underlined).
/// <para>
/// The test data (<see cref="LocalCollections" />, <see cref="RemoteTree" />, the Databases in
/// <see cref="BuildTreeAsync" />) and the expected text must not change when the tree builders change; only how
/// <see cref="BuildTreeAsync" /> feeds the data in may.
/// </para>
/// </summary>
public class CollectionTreeRenderingTests
{
    private static readonly DeviceId _laptop = DeviceId.FromCertificateBytes([1]);
    private static readonly DeviceId _desktop = DeviceId.FromCertificateBytes([2]);
    private static readonly DeviceId _tablet = DeviceId.FromCertificateBytes([3]);
    private static readonly RemoteDatabaseAddress _laptopClips = new(_laptop, Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly RemoteDatabaseAddress _desktopRecipes = new(_desktop, Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly RemoteDatabaseAddress _desktopNotes = new(_desktop, Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly RemoteDatabaseAddress _tabletClips = new(_tablet, Guid.Parse("44444444-4444-4444-4444-444444444444"));

    private static readonly Guid _inbox = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid _work = Guid.Parse("f0000000-0000-0000-0000-000000000001");
    private static readonly Guid _archive = Guid.Parse("f0000000-0000-0000-0000-000000000002");
    private static readonly Guid _drafts = Guid.Parse("f0000000-0000-0000-0000-000000000003");

    [Test]
    public async Task Tree_SortedBySortKey_RendersAsBefore()
    {
        // Act
        var tree = await BuildTreeAsync(sortAlphabetically: false);

        // Assert
        await Assert.That(tree).IsEqualTo(Expected(
            """
            💾 My Clips · Database · expanded
              📁 Notes · Collection · maroon
              📥 Inbox · Collection
                📁 Work · Folder
                  📁 Drafts · Folder
                📁 Archive · Folder
              🍳 Recipes · Collection
              🔒 Safe · Collection · underline
              🌊 Overflow · Collection
              📁 Virtual · VirtualCollectionsContainer
                🌐 Everything · VirtualCollection
                📅 Today · VirtualCollection
                🖼️ Bitmaps · VirtualCollection
              🗑️ Trashcan · VirtualCollection · maroon
              🔍 Search Results · VirtualCollection · maroon
            ❌ Archive · Database
            🖥️ Laptop clips — LAPTOP · RemoteDatabase
              📁 Loose · Collection
              📥 Inbox · Collection
                📁 Projects · Folder
                  📁 2026 · Folder
                📁 Old · Folder
              🔒 Safe · Collection
              📁 Virtual · VirtualCollectionsContainer
                🔍 Today · VirtualCollection
                🔍 Big clips · VirtualCollection
              🔍 Search: "budget" (0 results) · VirtualCollection
            🖥️ Recipes — DESKTOP (offline) · RemoteDatabase · unavailable
            🖥️ Notes — DESKTOP (offline · last seen 2 h ago) · RemoteDatabase · unavailable
            🖥️ Clips — TABLET (update required) · RemoteDatabase · unavailable
            """));
    }

    [Test]
    public async Task Tree_SortedAlphabetically_RendersAsBefore()
    {
        // Act
        var tree = await BuildTreeAsync(sortAlphabetically: true);

        // Assert
        await Assert.That(tree).IsEqualTo(Expected(
            """
            💾 My Clips · Database · expanded
              📥 Inbox · Collection
                📁 Work · Folder
                  📁 Drafts · Folder
                📁 Archive · Folder
              📁 Notes · Collection · maroon
              🌊 Overflow · Collection
              🍳 Recipes · Collection
              🔒 Safe · Collection · underline
              📁 Virtual · VirtualCollectionsContainer
                🖼️ Bitmaps · VirtualCollection
                🌐 Everything · VirtualCollection
                📅 Today · VirtualCollection
              🗑️ Trashcan · VirtualCollection · maroon
              🔍 Search Results · VirtualCollection · maroon
            ❌ Archive · Database
            🖥️ Laptop clips — LAPTOP · RemoteDatabase
              📥 Inbox · Collection
                📁 Projects · Folder
                  📁 2026 · Folder
                📁 Old · Folder
              📁 Loose · Collection
              🔒 Safe · Collection
              📁 Virtual · VirtualCollectionsContainer
                🔍 Big clips · VirtualCollection
                🔍 Today · VirtualCollection
              🔍 Search: "budget" (0 results) · VirtualCollection
            🖥️ Recipes — DESKTOP (offline) · RemoteDatabase · unavailable
            🖥️ Notes — DESKTOP (offline · last seen 2 h ago) · RemoteDatabase · unavailable
            🖥️ Clips — TABLET (update required) · RemoteDatabase · unavailable
            """));
    }

    /// <summary>
    /// A local Database's collection rows as stored: regular collections, Smart and SQL-based ones, read-only and Safe
    /// ones, one with its own icon, and the Inbox's folders (rows with the Folder type and a parent).
    /// </summary>
    private static List<Collection> LocalCollections() =>
    [
        new() { Id = _inbox, Title = "Inbox", SortKey = 20, AcceptNewClips = true, RetentionLimit = 200 },
        new() { Id = Guid.NewGuid(), Title = "Safe", SortKey = 40, AcceptNewClips = true, RetentionLimit = 0 },
        new() { Id = Guid.NewGuid(), Title = "Overflow", SortKey = 50, AcceptNewClips = true, RetentionLimit = 800 },
        new() { Id = Guid.NewGuid(), Title = "Notes", SortKey = 10, AcceptNewClips = true, ReadOnly = true, RetentionLimit = 200 },
        new() { Id = Guid.NewGuid(), Title = "Recipes", SortKey = 30, AcceptNewClips = true, RetentionLimit = 200, Icon = "🍳" },
        new() { Id = Guid.NewGuid(), Title = "Today", SortKey = 2, ListType = CollectionListType.Smart, RetentionLimit = 200 },
        new() { Id = Guid.NewGuid(), Title = "Everything", SortKey = 1, ListType = CollectionListType.SqlBased, RetentionLimit = 200 },
        new() { Id = Guid.NewGuid(), Title = "Bitmaps", SortKey = 3, LmType = CollectionLmType.Virtual, RetentionLimit = 200 },
        new() { Id = _work, Title = "Work", SortKey = 1, LmType = CollectionLmType.Folder, ParentId = _inbox, AcceptNewClips = true, RetentionLimit = 200 },
        new() { Id = _archive, Title = "Archive", SortKey = 2, LmType = CollectionLmType.Folder, ParentId = _inbox, AcceptNewClips = true, RetentionLimit = 200 },
        new() { Id = _drafts, Title = "Drafts", SortKey = 1, LmType = CollectionLmType.Folder, ParentId = _work, AcceptNewClips = true, RetentionLimit = 200 },
    ];

    /// <summary>
    /// The tree an online Peer serves for its Shared Database: collections, nested folders, a folder whose parent is
    /// missing, and Smart and SQL-based collections.
    /// </summary>
    private static List<CollectionEntry> RemoteTree()
    {
        var inbox = Guid.NewGuid();
        var projects = Guid.NewGuid();
        return
        [
            new CollectionEntry(inbox, null, "Inbox", CollectionEntryKind.Collection, 10),
            new CollectionEntry(Guid.NewGuid(), null, "Safe", CollectionEntryKind.Collection, 20),
            new CollectionEntry(Guid.NewGuid(), inbox, "Old", CollectionEntryKind.Folder, 2),
            new CollectionEntry(projects, inbox, "Projects", CollectionEntryKind.Folder, 1),
            new CollectionEntry(Guid.NewGuid(), projects, "2026", CollectionEntryKind.Folder, 1),
            new CollectionEntry(Guid.NewGuid(), Guid.NewGuid(), "Loose", CollectionEntryKind.Folder, 5),
            new CollectionEntry(Guid.NewGuid(), null, "Today", CollectionEntryKind.Smart, 30),
            new CollectionEntry(Guid.NewGuid(), null, "Big clips", CollectionEntryKind.SqlBased, 40),
        ];
    }

    /// <summary>
    /// Builds the tree from the test data the way ClipMate does, and writes it out. Two local Databases (the default one,
    /// and one whose file is missing), then four Remote Databases: one online with a Remote Database Alias and cached
    /// Remote Search results, two offline (never seen, and seen two hours ago) and one that needs an update.
    /// </summary>
    private static async Task<string> BuildTreeAsync(bool sortAlphabetically)
    {
        var configuration = new ClipMateConfiguration { DefaultDatabase = "db_main" };
        configuration.Preferences.SortCollectionsAlphabetically = sortAlphabetically;
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(configuration);

        // The local Database's collection rows: its tree as entries, and the rows themselves for its nodes
        var localCollections = LocalCollections();
        var collectionService = A.Fake<ICollectionService>();
        A.CallTo(() => collectionService.GetAllByDatabaseKeyAsync("db_main", A<CancellationToken>._)).Returns(localCollections);

        var databases = new FakeDatabases();
        databases.AddLocal("db_main", "My Clips").Tree = localCollections.Select(CollectionEntry.From).ToList();
        databases.AddLocal("db_archive", "Archive").IsAvailable = false;
        databases.AddRemote(new RemoteDatabase(_laptopClips, "Shared Clips", "LAPTOP", PeerConnectionStatus.Online, DateTime.Now) { Alias = "Laptop clips" })
            .Tree = RemoteTree();
        databases.AddRemote(new RemoteDatabase(_desktopRecipes, "Recipes", "DESKTOP", PeerConnectionStatus.Offline, null));
        databases.AddRemote(new RemoteDatabase(_desktopNotes, "Notes", "DESKTOP", PeerConnectionStatus.Offline, DateTime.Now.AddHours(-2)));
        databases.AddRemote(new RemoteDatabase(_tabletClips, "Clips", "TABLET", PeerConnectionStatus.UpdateRequired, DateTime.Now));

        var remoteSearchResults = new RemoteSearchResultsCache();
        remoteSearchResults.SetResults(_laptopClips, new RemoteSearchResult("budget", [], 0));

        using var viewModel = new CollectionTreeViewModel(collectionService, A.Fake<IFolderService>(), A.Fake<IClipService>(), configurationService,
            new StrongReferenceMessenger(), new CollectionTreeBuilder(configurationService, databases, collectionService,
                NullLogger<CollectionTreeBuilder>.Instance), NullLogger<CollectionTreeViewModel>.Instance, new SearchResultsCache(),
            A.Fake<IDatabaseIdService>(), A.Fake<IRemoteDatabaseService>(), databases, A.Fake<ISharedDatabaseService>(), A.Fake<IPeerDialogService>(),
            remoteSearchResults);

        await viewModel.LoadAsync();
        await viewModel.RefreshRemoteDatabasesAsync();

        var text = new StringBuilder();
        foreach (var item in viewModel.RootNodes)
            Write(text, item, 0);

        return text.ToString().TrimEnd();
    }

    private static void Write(StringBuilder text, TreeNodeBase node, int depth)
    {
        text.Append(' ', depth * 2).Append(node.Icon).Append(' ').Append(node.Name).Append(" · ").Append(node.NodeType);
        if (node.IsExpanded)
            text.Append(" · expanded");

        if (node.IsUnavailable)
            text.Append(" · unavailable");

        // The same property paths the tree's styles bind to, falling back as the bindings do
        var collection = node.GetType().GetProperty("Collection")?.GetValue(node);
        if (collection?.GetType().GetProperty(nameof(Collection.IsReadOnly))?.GetValue(collection) is true)
            text.Append(" · maroon");

        if (collection?.GetType().GetProperty(nameof(Collection.PurgePolicy))?.GetValue(collection) is PurgePolicy.Never)
            text.Append(" · underline");

        text.AppendLine();
        foreach (var item in node.Children)
            Write(text, item, depth + 1);
    }

    private static string Expected(string text) => text.ReplaceLineEndings(Environment.NewLine);
}
