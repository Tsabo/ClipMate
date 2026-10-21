using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Data.Repositories;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Paged clip lists, as served to Peers browsing a Shared Database.
/// </summary>
public class ClipRepositoryPagingTests : IntegrationTestBase
{
    private static readonly Guid _inbox = Guid.NewGuid();
    private static readonly Guid _projects = Guid.NewGuid();

    [Test]
    public async Task GetPageByCollectionAsync_FirstPage_ReturnsTheNewestClipsWithFormatFlagsAndTheTotal()
    {
        // Arrange
        await AddClipsAsync(_inbox, null, 5);
        await AddClipsAsync(_projects, null, 2);
        var repository = CreateRepository();

        // Act
        var page = await repository.GetPageByCollectionAsync(_inbox, 0, 2);

        // Assert
        await Assert.That(page.TotalCount).IsEqualTo(5);
        await Assert.That(page.Offset).IsEqualTo(0);
        await Assert.That(string.Join(",", page.Clips.Select(p => p.Title))).IsEqualTo("Clip 5,Clip 4");
        await Assert.That(page.Clips.All(p => p.HasText)).IsTrue();
        await Assert.That(page.HasMore).IsTrue();
    }

    [Test]
    public async Task GetPageByCollectionAsync_LastPage_HasNoMore()
    {
        // Arrange
        await AddClipsAsync(_inbox, null, 5);
        var repository = CreateRepository();

        // Act
        var page = await repository.GetPageByCollectionAsync(_inbox, 4, 2);

        // Assert
        await Assert.That(string.Join(",", page.Clips.Select(p => p.Title))).IsEqualTo("Clip 1");
        await Assert.That(page.HasMore).IsFalse();
    }

    [Test]
    public async Task GetPageByFolderAsync_ReturnsOnlyTheFolderClips()
    {
        // Arrange
        var folder = Guid.NewGuid();
        await AddClipsAsync(_inbox, null, 3);
        await AddClipsAsync(_inbox, folder, 2);
        var repository = CreateRepository();

        // Act
        var page = await repository.GetPageByFolderAsync(folder, 0, 10);

        // Assert
        await Assert.That(page.TotalCount).IsEqualTo(2);
        await Assert.That(page.Clips.All(p => p.FolderId == folder)).IsTrue();
    }

    private ClipRepository CreateRepository() => new(DbContext, A.Fake<ILogger<ClipRepository>>());

    private async Task AddClipsAsync(Guid collectionId, Guid? folderId, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                FolderId = folderId,
                Title = $"Clip {i}",
                SortKey = i,
                CapturedAt = DateTimeOffset.UtcNow,
                Type = ClipType.Text,
                ContentHash = Guid.NewGuid().ToString("N"),
            };

            DbContext.Clips.Add(clip);
            DbContext.ClipData.Add(new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                Format = Formats.UnicodeText.Code,
                FormatName = Formats.UnicodeText.Name,
                StorageType = StorageType.Text,
            });
        }

        await DbContext.SaveChangesAsync();
    }
}
