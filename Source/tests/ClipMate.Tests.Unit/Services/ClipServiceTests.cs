using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ClipService (Test-Driven Development).
/// These tests define expected behavior before implementation.
/// </summary>
public class ClipServiceTests
{
    private const string _testDatabaseKey = "db_test0001";
    private readonly Mock<IDatabaseContextFactory> _mockContextFactory;
    private readonly Mock<ILogger<ClipService>> _mockLogger;
    private readonly Mock<IClipRepository> _mockRepository;

    public ClipServiceTests()
    {
        _mockRepository = new Mock<IClipRepository>();
        _mockContextFactory = new Mock<IDatabaseContextFactory>();
        _mockLogger = new Mock<ILogger<ClipService>>();

        // Setup factory to return our mock repository
        _mockContextFactory.Setup(p => p.GetClipRepository(It.IsAny<string>()))
            .Returns(_mockRepository.Object);
    }

    [Test]
    public async Task GetByIdAsync_WithValidId_ShouldReturnClip()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var expectedClip = CreateTestClip(clipId);
        _mockRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClip);

        var service = CreateClipService();

        // Act
        var result = await service.GetByIdAsync(_testDatabaseKey, clipId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(clipId);
    }

    [Test]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        _mockRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        var service = CreateClipService();

        // Act
        var result = await service.GetByIdAsync(_testDatabaseKey, clipId);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetRecentAsync_ShouldReturnClipsOrderedByDateDescending()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var clips = new List<Clip>
        {
            CreateTestClip(Guid.NewGuid(), now), // Most recent
            CreateTestClip(Guid.NewGuid(), now.AddMinutes(-1)), // Second
            CreateTestClip(Guid.NewGuid(), now.AddMinutes(-2)), // Oldest
        };

        _mockRepository.Setup(p => p.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips); // Already ordered descending

        var service = CreateClipService();

        // Act
        var result = await service.GetRecentAsync(_testDatabaseKey, 10);

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].CapturedAt).IsEqualTo(now);
        await Assert.That(result[1].CapturedAt).IsEqualTo(now.AddMinutes(-1));
        await Assert.That(result[2].CapturedAt).IsEqualTo(now.AddMinutes(-2));
    }

    [Test]
    public async Task GetRecentAsync_WithCount_ShouldLimitResults()
    {
        // Arrange
        var clips = Enumerable.Range(0, 5)
            .Select(_ => CreateTestClip(Guid.NewGuid()))
            .ToList();

        _mockRepository.Setup(p => p.GetRecentAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips.Take(3).ToList());

        var service = CreateClipService();

        // Act
        var result = await service.GetRecentAsync(_testDatabaseKey, 3);

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        _mockRepository.Verify(p => p.GetRecentAsync(3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateAsync_WithValidClip_ShouldSaveAndReturnClip()
    {
        // Arrange
        var clip = CreateTestClip(Guid.NewGuid());
        _mockRepository.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        var service = CreateClipService();

        // Act
        var result = await service.CreateAsync(_testDatabaseKey, clip);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsEqualTo(clip.Id);
        _mockRepository.Verify(p => p.CreateAsync(clip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateAsync_WithDuplicateContentHash_ShouldNotSaveDuplicate()
    {
        // Arrange
        var existingClip = CreateTestClip(Guid.NewGuid(), contentHash: "DUPLICATE_HASH");
        var newClip = CreateTestClip(Guid.NewGuid(), contentHash: "DUPLICATE_HASH");

        _mockRepository.Setup(p => p.GetByContentHashAsync("DUPLICATE_HASH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingClip);

        var service = CreateClipService();

        // Act
        var result = await service.CreateAsync(_testDatabaseKey, newClip);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsEqualTo(existingClip.Id); // Should return existing clip
        _mockRepository.Verify(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task UpdateAsync_WithValidClip_ShouldUpdateSuccessfully()
    {
        // Arrange
        var clip = CreateTestClip(Guid.NewGuid());
        _mockRepository.Setup(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateClipService();

        // Act
        await service.UpdateAsync(_testDatabaseKey, clip);

        // Assert
        _mockRepository.Verify(p => p.UpdateAsync(clip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_WithValidId_ShouldDeleteSuccessfully()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        _mockRepository.Setup(p => p.DeleteAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateClipService();

        // Act
        await service.DeleteAsync(_testDatabaseKey, clipId);

        // Assert
        _mockRepository.Verify(p => p.DeleteAsync(clipId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetByCollectionAsync_ShouldReturnClipsInCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var clips = new List<Clip>
        {
            CreateTestClip(Guid.NewGuid(), collectionId: collectionId),
            CreateTestClip(Guid.NewGuid(), collectionId: collectionId),
        };

        _mockRepository.Setup(p => p.GetByCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        var service = CreateClipService();

        // Act
        var result = await service.GetByCollectionAsync(_testDatabaseKey, collectionId);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(p => p.CollectionId == collectionId)).IsTrue();
    }

    [Test]
    public async Task GetFavoritesAsync_ShouldReturnOnlyFavoriteClips()
    {
        // Arrange
        var clips = new List<Clip>
        {
            CreateTestClip(Guid.NewGuid(), isFavorite: true),
            CreateTestClip(Guid.NewGuid(), isFavorite: true),
        };

        _mockRepository.Setup(p => p.GetFavoritesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        var service = CreateClipService();

        // Act
        var result = await service.GetFavoritesAsync(_testDatabaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(p => p.IsFavorite)).IsTrue();
    }

    [Test]
    public async Task CreateAsync_WithNullClip_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateClipService();

        // Act & Assert
        await Assert.That(async () => await service.CreateAsync(_testDatabaseKey, null!)).Throws<ArgumentNullException>();
    }

    private IClipService CreateClipService() =>
        // Create ClipService with factory
        new ClipService(
            _mockContextFactory.Object,
            Mock.Of<IConfigurationService>(),
            Mock.Of<IClipboardService>(),
            Mock.Of<ITemplateService>(),
            _mockLogger.Object);

    private Clip CreateTestClip(Guid id,
        DateTime? capturedAt = null,
        Guid? collectionId = null,
        string contentHash = "TEST_HASH",
        bool isFavorite = false) =>
        new()
        {
            Id = id,
            Type = ClipType.Text,
            TextContent = "Test content",
            ContentHash = contentHash,
            CapturedAt = capturedAt ?? DateTime.UtcNow,
            CollectionId = collectionId ?? Guid.NewGuid(),
            IsFavorite = isFavorite,
        };
}
