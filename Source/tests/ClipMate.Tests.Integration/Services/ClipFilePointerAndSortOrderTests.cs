using System.Text.Json;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for converting Files clips to text (Edit > Convert File Pointer To Text)
/// and for manually reordering clips via SortKey (Move To Top/Bottom Of Sort Order).
/// </summary>
public class ClipFilePointerAndSortOrderTests : IntegrationTestBase
{
    private const string _testDatabaseKey = "db_test0001";

    [Test]
    public async Task ConvertFilePointerToTextAsync_WithFilesClipAndExistingTextBlob_ShouldSwitchToTextType()
    {
        // Arrange
        var clipService = CreateClipService();
        var filePaths = new List<string> { @"C:\Docs\hello.txt", @"C:\Docs\world.txt" };
        var filePathsJson = JsonSerializer.Serialize(filePaths);
        var clip = new Clip
        {
            Type = ClipType.Files,
            FilePathsJson = filePathsJson,
            TextContent = string.Join(Environment.NewLine, filePaths),
            ContentHash = "files_hash_1",
            CapturedAt = DateTime.UtcNow,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await clipService.ConvertFilePointerToTextAsync(_testDatabaseKey, savedClip.Id);
        await DbContext.SaveChangesAsync();

        // Assert
        await Assert.That(result).IsTrue();

        var retrievedClip = await clipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(retrievedClip).IsNotNull();
        await Assert.That(retrievedClip!.Type).IsEqualTo(ClipType.Text);
        await Assert.That(retrievedClip.HasFiles).IsFalse();
        await Assert.That(retrievedClip.HasText).IsTrue();

        var formats = await clipService.GetClipFormatsAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(formats.Any(p => p.FormatName == Formats.HDrop.Name)).IsFalse();
        await Assert.That(formats.Any(p => p.FormatName == Formats.UnicodeText.Name)).IsTrue();
    }

    [Test]
    public async Task ConvertFilePointerToTextAsync_WithoutExistingTextBlob_ShouldDeriveTextFromFilePaths()
    {
        // Arrange - simulates a Files clip captured with AutoExpandHdropFilePointers off and no search text stored
        var clipService = CreateClipService();
        var filePaths = new List<string> { @"C:\Docs\only-file.txt" };
        var clip = new Clip
        {
            Type = ClipType.Files,
            FilePathsJson = JsonSerializer.Serialize(filePaths),
            ContentHash = "files_hash_2",
            CapturedAt = DateTime.UtcNow,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await clipService.ConvertFilePointerToTextAsync(_testDatabaseKey, savedClip.Id);
        await DbContext.SaveChangesAsync();

        // Assert
        await Assert.That(result).IsTrue();

        var formats = await clipService.GetClipFormatsAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(formats.Any(p => p.FormatName == Formats.UnicodeText.Name)).IsTrue();
    }

    [Test]
    public async Task ConvertFilePointerToTextAsync_WithNonFilesClip_ShouldReturnFalse()
    {
        // Arrange
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Just text",
            ContentHash = "text_hash_1",
            CapturedAt = DateTime.UtcNow,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await clipService.ConvertFilePointerToTextAsync(_testDatabaseKey, savedClip.Id);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MoveClipSortOrderAsync_ToTop_ShouldExceedAllSiblingSortKeys()
    {
        // Arrange
        var clipService = CreateClipService();
        var collectionId = Guid.NewGuid();

        var first = await clipService.CreateAsync(_testDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            TextContent = "First",
            ContentHash = "sort_hash_1",
            CollectionId = collectionId,
            CapturedAt = DateTime.UtcNow,
        });

        var second = await clipService.CreateAsync(_testDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            TextContent = "Second",
            ContentHash = "sort_hash_2",
            CollectionId = collectionId,
            CapturedAt = DateTime.UtcNow,
        });

        var third = await clipService.CreateAsync(_testDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            TextContent = "Third",
            ContentHash = "sort_hash_3",
            CollectionId = collectionId,
            CapturedAt = DateTime.UtcNow,
        });

        await DbContext.SaveChangesAsync();

        // Act - move the first (oldest, lowest SortKey) clip to the top
        var result = await clipService.MoveClipSortOrderAsync(_testDatabaseKey, first.Id, true);
        await DbContext.SaveChangesAsync();

        // Assert
        await Assert.That(result).IsTrue();

        var movedClip = await clipService.GetByIdAsync(_testDatabaseKey, first.Id);
        await Assert.That(movedClip!.SortKey).IsGreaterThan(second.SortKey);
        await Assert.That(movedClip.SortKey).IsGreaterThan(third.SortKey);
    }

    [Test]
    public async Task MoveClipSortOrderAsync_ToBottom_ShouldBeBelowAllSiblingSortKeys()
    {
        // Arrange
        var clipService = CreateClipService();
        var collectionId = Guid.NewGuid();

        var first = await clipService.CreateAsync(_testDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            TextContent = "First",
            ContentHash = "sort_hash_4",
            CollectionId = collectionId,
            CapturedAt = DateTime.UtcNow,
        });

        var second = await clipService.CreateAsync(_testDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            TextContent = "Second",
            ContentHash = "sort_hash_5",
            CollectionId = collectionId,
            CapturedAt = DateTime.UtcNow,
        });

        await DbContext.SaveChangesAsync();

        // Act - move the most recently captured (highest SortKey) clip to the bottom
        var result = await clipService.MoveClipSortOrderAsync(_testDatabaseKey, second.Id, false);
        await DbContext.SaveChangesAsync();

        // Assert
        await Assert.That(result).IsTrue();

        var movedClip = await clipService.GetByIdAsync(_testDatabaseKey, second.Id);
        await Assert.That(movedClip!.SortKey).IsLessThan(first.SortKey);
    }

    [Test]
    public async Task MoveClipSortOrderAsync_WithNoSiblings_ShouldLeaveSortKeyUnchanged()
    {
        // Arrange
        var clipService = CreateClipService();
        var clip = await clipService.CreateAsync(_testDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            TextContent = "Only clip",
            ContentHash = "sort_hash_6",
            CollectionId = Guid.NewGuid(),
            CapturedAt = DateTime.UtcNow,
        });

        await DbContext.SaveChangesAsync();
        var originalSortKey = clip.SortKey;

        // Act
        var result = await clipService.MoveClipSortOrderAsync(_testDatabaseKey, clip.Id, true);

        // Assert
        await Assert.That(result).IsTrue();

        var unchangedClip = await clipService.GetByIdAsync(_testDatabaseKey, clip.Id);
        await Assert.That(unchangedClip!.SortKey).IsEqualTo(originalSortKey);
    }

    /// <summary>
    /// Creates a clip service backed by a real ClipRepository over the shared test DbContext.
    /// </summary>
    private IClipService CreateClipService()
    {
        var contextFactory = new Mock<IDatabaseContextFactory>();

        var logger = Mock.Of<ILogger<ClipRepository>>();
        var repository = new ClipRepository(DbContext, logger);
        contextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey))
            .Returns(repository);

        var serviceLogger = Mock.Of<ILogger<ClipService>>();

        return new ClipService(
            contextFactory.Object,
            Mock.Of<IConfigurationService>(),
            Mock.Of<IClipboardService>(),
            Mock.Of<ITemplateService>(),
            Mock.Of<IEncryptionService>(),
            Mock.Of<IDecryptedBlobCacheService>(),
            Mock.Of<IMessenger>(),
            serviceLogger);
    }
}
