using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// The local Databases' part of the collection tree.
/// </summary>
public class CollectionTreeBuilderTests
{
    private string _databaseFile = null!;

    [Before(Test)]
    public void Setup() => _databaseFile = Path.GetTempFileName();

    [After(Test)]
    public void Cleanup() => File.Delete(_databaseFile);

    [Test]
    public async Task BuildTreeAsync_FolderInACollection_IsShownOnlyUnderThatCollection()
    {
        // Arrange: as stored, a folder is a collection row with the Folder type and a parent, and the collection list
        // includes it
        var inbox = new Collection { Id = Guid.NewGuid(), Title = "Inbox", SortKey = 100, AcceptNewClips = true };
        var safe = new Collection { Id = Guid.NewGuid(), Title = "Safe", SortKey = 200, LmType = CollectionLmType.Folder };
        var work = new Collection { Id = Guid.NewGuid(), Title = "Work", SortKey = 10, LmType = CollectionLmType.Folder, ParentId = inbox.Id };

        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration> { ["db"] = new() { Name = "My Clips", FilePath = _databaseFile } },
        });

        var collectionService = A.Fake<ICollectionService>();
        A.CallTo(() => collectionService.GetAllByDatabaseKeyAsync("db", A<CancellationToken>._)).Returns([inbox, safe, work]);

        var folderService = A.Fake<IFolderService>();
        A.CallTo(() => folderService.GetRootFoldersAsync(inbox.Id, A<CancellationToken>._))
            .Returns([new Folder { Id = work.Id, Name = "Work", CollectionId = inbox.Id }]);

        var builder = new CollectionTreeBuilder(configurationService, folderService, collectionService, NullLogger<CollectionTreeBuilder>.Instance);

        // Act
        var database = (await builder.BuildTreeAsync(TreeNodeType.SpecialCollection)).Single();

        // Assert
        await Assert.That(string.Join(", ", database.Children.Select(p => p.Name))).IsEqualTo("Inbox, Safe");
        var inboxNode = database.Children.OfType<CollectionTreeNode>().Single(p => p.Name == "Inbox");
        await Assert.That(string.Join(", ", inboxNode.Children.Select(p => p.Name))).IsEqualTo("Work");
    }
}
