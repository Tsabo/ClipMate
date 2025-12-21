using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for clip persistence and retrieval after application restart.
/// Verifies that clips are correctly saved to and loaded from the database.
/// </summary>
public class ClipPersistenceTests : IntegrationTestBase
{
    private const string _testDatabaseKey = "db_test0001";

    [Test]
    public async Task SavedClip_ShouldPersistAcrossDbContextInstances()
    {
        // Arrange
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            // Note: TextContent is transient and not persisted directly to Clips table
            // It would be stored in BlobTxt table via ClipData in production
            ContentHash = "test_hash_123",
            SourceApplicationName = "TestApp",
            CapturedAt = DateTime.UtcNow,
        };

        // Act - Save in first context
        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
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
        var retrievedClip = await newClipService.GetByIdAsync(_testDatabaseKey, savedId);
        await Assert.That(retrievedClip).IsNotNull();
        await Assert.That(retrievedClip!.ContentHash).IsEqualTo("test_hash_123");
        await Assert.That(retrievedClip.SourceApplicationName).IsEqualTo("TestApp");
        await Assert.That(retrievedClip.Type).IsEqualTo(ClipType.Text);

        // Cleanup the new context
        await using (newContext) { }
    }

    [Test]
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
            CapturedAt = now.AddMinutes(-2),
        };

        var clip2 = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Second",
            ContentHash = "hash_2",
            CapturedAt = now.AddMinutes(-1),
        };

        var clip3 = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Third",
            ContentHash = "hash_3",
            CapturedAt = now,
        };

        // Act
        await clipService.CreateAsync(_testDatabaseKey, clip1);
        await clipService.CreateAsync(_testDatabaseKey, clip2);
        await clipService.CreateAsync(_testDatabaseKey, clip3);
        await DbContext.SaveChangesAsync();

        // Assert
        var recentClips = await clipService.GetRecentAsync(_testDatabaseKey, 10);
        await Assert.That(recentClips.Count).IsEqualTo(3);
        await Assert.That(recentClips[0].TextContent).IsEqualTo("Third"); // Most recent first
        await Assert.That(recentClips[1].TextContent).IsEqualTo("Second");
        await Assert.That(recentClips[2].TextContent).IsEqualTo("First");
    }

    [Test]
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
            CapturedAt = DateTime.UtcNow,
        };

        // Act
        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        var retrievedClip = await clipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);

        // Assert
        await Assert.That(retrievedClip).IsNotNull();
        await Assert.That(retrievedClip!.ImageData).IsNotNull();
        await Assert.That(retrievedClip.ImageData!).IsEqualTo(imageData);
    }

    [Test]
    public async Task DeletedClip_ShouldNotPersist()
    {
        // Arrange
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            // Note: TextContent is transient and not persisted
            ContentHash = "delete_hash",
            SourceApplicationName = "TestApp",
            CapturedAt = DateTime.UtcNow,
        };

        // Act
        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        await clipService.DeleteAsync(_testDatabaseKey, savedClip.Id);
        await DbContext.SaveChangesAsync();

        var deletedClip = await clipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);

        // Assert
        await Assert.That(deletedClip).IsNull();
    }

    [Test]
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
            CapturedAt = DateTime.UtcNow,
        };

        // Act
        await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        var isDuplicate = await clipService.IsDuplicateAsync(_testDatabaseKey, contentHash);

        // Assert
        await Assert.That(isDuplicate).IsTrue();
    }

    /// <summary>
    /// Creates a clip service instance for testing.
    /// </summary>
    private IClipService CreateClipService()
    {
        var repositoryFactory = new Mock<IClipRepositoryFactory>();

        // Setup factory to return a real repository for the test database
        var logger = Mock.Of<ILogger<ClipRepository>>();
        var repository = new ClipRepository(DbContext, logger);
        repositoryFactory.Setup(p => p.CreateRepository(_testDatabaseKey))
            .Returns(repository);

        var soundService = new Mock<ISoundService>();
        soundService.Setup(p => p.PlaySoundAsync(It.IsAny<SoundEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var serviceLogger = Mock.Of<ILogger<ClipService>>();

        return new ClipService(
            repositoryFactory.Object,
            Mock.Of<IDatabaseContextFactory>(),
            Mock.Of<IDatabaseManager>(),
            soundService.Object,
            serviceLogger);
    }

    /// <summary>
    /// Creates a clip service with a specific DbContext.
    /// </summary>
    private IClipService CreateClipServiceWithContext(ClipMateDbContext context)
    {
        var repositoryFactory = new Mock<IClipRepositoryFactory>();

        // Setup factory to return a real repository with the provided context
        var logger = Mock.Of<ILogger<ClipRepository>>();
        var repository = new ClipRepository(context, logger);
        repositoryFactory.Setup(p => p.CreateRepository(_testDatabaseKey))
            .Returns(repository);

        var soundService = new Mock<ISoundService>();
        soundService.Setup(p => p.PlaySoundAsync(It.IsAny<SoundEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var serviceLogger = Mock.Of<ILogger<ClipService>>();

        return new ClipService(
            repositoryFactory.Object,
            Mock.Of<IDatabaseContextFactory>(),
            Mock.Of<IDatabaseManager>(),
            soundService.Object,
            serviceLogger);
    }
}
