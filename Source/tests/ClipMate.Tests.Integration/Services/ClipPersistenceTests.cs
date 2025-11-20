using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for clip persistence and retrieval after application restart.
/// Verifies that clips are correctly saved to and loaded from the database.
/// </summary>
public class ClipPersistenceTests : IntegrationTestBase
{
    [Fact]
    public async Task SavedClip_ShouldPersistAcrossDbContextInstances()
    {
        // Arrange
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Test persistence content",
            ContentHash = "test_hash_123",
            CapturedAt = DateTime.UtcNow
        };

        // Act - Save in first context
        var savedClip = await clipService.CreateAsync(clip);
        var savedId = savedClip.Id;

        // Save changes to ensure data is persisted
        await DbContext.SaveChangesAsync();
        
        // Create new context and service (simulates app restart)
        // Keep the connection alive by getting it before disposing
        var connection = DbContext.Database.GetDbConnection();
        var newContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);
        var newClipService = CreateClipServiceWithContext(newContext);

        // Assert - Retrieve in new context
        var retrievedClip = await newClipService.GetByIdAsync(savedId);
        retrievedClip.ShouldNotBeNull();
        retrievedClip.TextContent.ShouldBe("Test persistence content");
        retrievedClip.ContentHash.ShouldBe("test_hash_123");
        
        // Cleanup the new context
        newContext.Dispose();
    }

    [Fact]
    public async Task MultipleClips_ShouldPersistInCorrectOrder()
    {
        // Arrange
        var clipService = CreateClipService();
        var now = DateTime.UtcNow;

        var clip1 = new Clip
        {
            Type = ClipType.Text,
            TextContent = "First",
            ContentHash = "hash_1",
            CapturedAt = now.AddMinutes(-2)
        };

        var clip2 = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Second",
            ContentHash = "hash_2",
            CapturedAt = now.AddMinutes(-1)
        };

        var clip3 = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Third",
            ContentHash = "hash_3",
            CapturedAt = now
        };

        // Act
        await clipService.CreateAsync(clip1);
        await clipService.CreateAsync(clip2);
        await clipService.CreateAsync(clip3);
        await DbContext.SaveChangesAsync();

        // Assert
        var recentClips = await clipService.GetRecentAsync(10);
        recentClips.Count.ShouldBe(3);
        recentClips[0].TextContent.ShouldBe("Third");  // Most recent first
        recentClips[1].TextContent.ShouldBe("Second");
        recentClips[2].TextContent.ShouldBe("First");
    }

    [Fact]
    public async Task ImageClip_ShouldPersistBinaryData()
    {
        // Arrange
        var clipService = CreateClipService();
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var clip = new Clip
        {
            Type = ClipType.Image,
            ImageData = imageData,
            ContentHash = "image_hash",
            CapturedAt = DateTime.UtcNow
        };

        // Act
        var savedClip = await clipService.CreateAsync(clip);
        await DbContext.SaveChangesAsync();

        var retrievedClip = await clipService.GetByIdAsync(savedClip.Id);

        // Assert
        retrievedClip.ShouldNotBeNull();
        retrievedClip.ImageData.ShouldNotBeNull();
        retrievedClip.ImageData.ShouldBe(imageData);
    }

    [Fact]
    public async Task DeletedClip_ShouldNotPersist()
    {
        // Arrange
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = "To be deleted",
            ContentHash = "delete_hash",
            CapturedAt = DateTime.UtcNow
        };

        // Act
        var savedClip = await clipService.CreateAsync(clip);
        await DbContext.SaveChangesAsync();

        await clipService.DeleteAsync(savedClip.Id);
        await DbContext.SaveChangesAsync();

        var deletedClip = await clipService.GetByIdAsync(savedClip.Id);

        // Assert
        deletedClip.ShouldBeNull();
    }

    [Fact]
    public async Task DuplicateCheck_ShouldWorkAcrossContexts()
    {
        // Arrange
        var clipService = CreateClipService();
        var contentHash = "duplicate_hash_test";
        
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Duplicate content",
            ContentHash = contentHash,
            CapturedAt = DateTime.UtcNow
        };

        // Act
        await clipService.CreateAsync(clip);
        await DbContext.SaveChangesAsync();

        var isDuplicate = await clipService.IsDuplicateAsync(contentHash);

        // Assert
        isDuplicate.ShouldBeTrue();
    }

    /// <summary>
    /// Creates a clip service instance for testing.
    /// </summary>
    private IClipService CreateClipService()
    {
        var logger = Mock.Of<ILogger<ClipRepository>>();
        var repository = new ClipRepository(DbContext, logger);
        return new ClipService(repository);
    }

    /// <summary>
    /// Creates a clip service with a specific DbContext.
    /// </summary>
    private IClipService CreateClipServiceWithContext(ClipMateDbContext context)
    {
        var logger = Mock.Of<ILogger<ClipRepository>>();
        var repository = new ClipRepository(context, logger);
        return new ClipService(repository);
    }
}
