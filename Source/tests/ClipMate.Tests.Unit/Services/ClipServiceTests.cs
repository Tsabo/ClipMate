using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Shouldly;
using Moq;
using Xunit;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ClipService (Test-Driven Development).
/// These tests define expected behavior before implementation.
/// </summary>
public class ClipServiceTests
{
    private readonly Mock<IClipRepository> _mockRepository;

    public ClipServiceTests()
    {
        _mockRepository = new Mock<IClipRepository>();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnClip()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var expectedClip = CreateTestClip(clipId);
        _mockRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClip);
        
        var service = CreateClipService();

        // Act
        var result = await service.GetByIdAsync(clipId);

        // Assert
        result.ShouldNotBeNull();
        result!.Id.ShouldBe(clipId);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        _mockRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);
        
        var service = CreateClipService();

        // Act
        var result = await service.GetByIdAsync(clipId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetRecentAsync_ShouldReturnClipsOrderedByDateDescending()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var clips = new List<Clip>
        {
            CreateTestClip(Guid.NewGuid(), now),              // Most recent
            CreateTestClip(Guid.NewGuid(), now.AddMinutes(-1)), // Second
            CreateTestClip(Guid.NewGuid(), now.AddMinutes(-2))  // Oldest
        };
        
        _mockRepository.Setup(p => p.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips); // Already ordered descending
        
        var service = CreateClipService();

        // Act
        var result = await service.GetRecentAsync(10);

        // Assert
        result.Count.ShouldBe(3);
        result[0].CapturedAt.ShouldBe(now);
        result[1].CapturedAt.ShouldBe(now.AddMinutes(-1));
        result[2].CapturedAt.ShouldBe(now.AddMinutes(-2));
    }

    [Fact]
    public async Task GetRecentAsync_WithCount_ShouldLimitResults()
    {
        // Arrange
        var clips = Enumerable.Range(0, 5)
            .Select(i => CreateTestClip(Guid.NewGuid()))
            .ToList();
        
        _mockRepository.Setup(p => p.GetRecentAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips.Take(3).ToList());
        
        var service = CreateClipService();

        // Act
        var result = await service.GetRecentAsync(3);

        // Assert
        result.Count.ShouldBe(3);
        _mockRepository.Verify(p => p.GetRecentAsync(3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithValidClip_ShouldSaveAndReturnClip()
    {
        // Arrange
        var clip = CreateTestClip(Guid.NewGuid());
        _mockRepository.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);
        
        var service = CreateClipService();

        // Act
        var result = await service.CreateAsync(clip);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(clip.Id);
        _mockRepository.Verify(p => p.CreateAsync(clip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateContentHash_ShouldNotSaveDuplicate()
    {
        // Arrange
        var existingClip = CreateTestClip(Guid.NewGuid(), contentHash: "DUPLICATE_HASH");
        var newClip = CreateTestClip(Guid.NewGuid(), contentHash: "DUPLICATE_HASH");
        
        _mockRepository.Setup(p => p.GetByContentHashAsync("DUPLICATE_HASH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingClip);
        
        var service = CreateClipService();

        // Act
        var result = await service.CreateAsync(newClip);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(existingClip.Id); // Should return existing clip
        _mockRepository.Verify(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithValidClip_ShouldUpdateSuccessfully()
    {
        // Arrange
        var clip = CreateTestClip(Guid.NewGuid());
        _mockRepository.Setup(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        var service = CreateClipService();

        // Act
        await service.UpdateAsync(clip);

        // Assert
        _mockRepository.Verify(p => p.UpdateAsync(clip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_ShouldDeleteSuccessfully()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        _mockRepository.Setup(p => p.DeleteAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        var service = CreateClipService();

        // Act
        await service.DeleteAsync(clipId);

        // Assert
        _mockRepository.Verify(p => p.DeleteAsync(clipId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByCollectionAsync_ShouldReturnClipsInCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var clips = new List<Clip>
        {
            CreateTestClip(Guid.NewGuid(), collectionId: collectionId),
            CreateTestClip(Guid.NewGuid(), collectionId: collectionId)
        };
        
        _mockRepository.Setup(p => p.GetByCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);
        
        var service = CreateClipService();

        // Act
        var result = await service.GetByCollectionAsync(collectionId);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(clip => clip.CollectionId == collectionId);
    }

    [Fact]
    public async Task GetFavoritesAsync_ShouldReturnOnlyFavoriteClips()
    {
        // Arrange
        var clips = new List<Clip>
        {
            CreateTestClip(Guid.NewGuid(), isFavorite: true),
            CreateTestClip(Guid.NewGuid(), isFavorite: true)
        };
        
        _mockRepository.Setup(p => p.GetFavoritesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);
        
        var service = CreateClipService();

        // Act
        var result = await service.GetFavoritesAsync();

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(clip => clip.IsFavorite == true);
    }

    [Fact]
    public async Task CreateAsync_WithNullClip_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateClipService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () => await service.CreateAsync(null!));
    }

    private IClipService CreateClipService()
    {
        // Now using real ClipService implementation with mocked repository
        return new ClipService(_mockRepository.Object);
    }

    private Clip CreateTestClip(
        Guid id,
        DateTime? capturedAt = null,
        Guid? collectionId = null,
        string contentHash = "TEST_HASH",
        bool isFavorite = false)
    {
        return new Clip
        {
            Id = id,
            Type = ClipType.Text,
            TextContent = "Test content",
            ContentHash = contentHash,
            CapturedAt = capturedAt ?? DateTime.UtcNow,
            CollectionId = collectionId ?? Guid.NewGuid(),
            IsFavorite = isFavorite
        };
    }
}
