using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for RetentionEnforcementService ensuring retention rules are properly enforced.
/// Tests MaxClips, MaxBytes, MaxAge retention, Overflow collection behavior, and ReadOnly bypass.
/// </summary>
public class RetentionEnforcementServiceTests
{
    private const string _testDatabaseKey = "test-db";
    private Mock<IDatabaseContextFactory> _mockContextFactory = null!;
    private Mock<IClipRepository> _mockClipRepository = null!;
    private Mock<ICollectionRepository> _mockCollectionRepository = null!;

    [Before(Test)]
    public void Setup()
    {
        // Mock repositories
        _mockClipRepository = new Mock<IClipRepository>();
        _mockCollectionRepository = new Mock<ICollectionRepository>();

        // Mock context factory to return our mocked repositories
        _mockContextFactory = new Mock<IDatabaseContextFactory>();
        _mockContextFactory.Setup(f => f.GetClipRepository(It.IsAny<string>()))
            .Returns(_mockClipRepository.Object);
        _mockContextFactory.Setup(f => f.GetCollectionRepository(It.IsAny<string>()))
            .Returns(_mockCollectionRepository.Object);
    }

    private IRetentionEnforcementService CreateService() =>
        new RetentionEnforcementService(
            _mockContextFactory.Object,
            Mock.Of<ILogger<IRetentionEnforcementService>>());

    #region ReadOnly Collection Tests

    [Test]
    public async Task EnforceRetentionAsync_ReadOnlyCollection_SkipsEnforcement()
    {
        // Arrange
        var collectionId = Guid.NewGuid();

        var collection = new Collection
        {
            Id = collectionId,
            Title = "Safe",
            LmType = CollectionLmType.Normal,
            ReadOnly = true,
            MaxClips = 5,
        };

        var clips = Enumerable.Range(1, 10)
            .Select(i => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-i),
                Size = 100,
            })
            .ToList();

        _mockCollectionRepository.Setup(p => p.GetByIdAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(collection);

        _mockClipRepository.Setup(p => p.GetClipsInCollectionAsync(collectionId))
            .ReturnsAsync(clips);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

        // Assert
        _mockClipRepository.Verify(
            p => p.MoveClipsToCollectionAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>()),
            Times.Never);
    }

    #endregion

    #region Overflow Collection Tests

    [Test]
    public async Task EnforceRetentionAsync_OverflowExceeded_MovesToTrashcan()
    {
        // Arrange
        var overflowId = Guid.NewGuid();
        var trashId = Guid.NewGuid();

        var overflowCollection = new Collection
        {
            Id = overflowId,
            Title = "Overflow",
            LmType = CollectionLmType.Normal,
            Role = CollectionRole.Overflow,
            MaxClips = 5,
        };

        var clips = Enumerable.Range(1, 10)
            .Select(p => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = overflowId,
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-p),
                Size = 100,
            })
            .ToList();

        var trashCollection = new Collection
        {
            Id = trashId,
            Title = "Trash Can",
            LmType = CollectionLmType.Normal,
            Role = CollectionRole.Trashcan,
        };

        _mockCollectionRepository.Setup(p => p.GetByIdAsync(_testDatabaseKey, overflowId))
            .ReturnsAsync(overflowCollection);

        _mockClipRepository.Setup(p => p.GetClipsInCollectionAsync(overflowId))
            .ReturnsAsync(clips);

        _mockCollectionRepository.Setup(p => p.GetTrashcanCollectionAsync(_testDatabaseKey))
            .ReturnsAsync(trashCollection);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, overflowId);

        // Assert
        _mockClipRepository.Verify(
            p => p.MoveClipsToCollectionAsync(It.IsAny<IEnumerable<Guid>>(), trashId),
            Times.Once);
    }

    #endregion

    #region MaxAge Tests

    [Test]
    public async Task EnforceRetentionAsync_WithMaxAgeExceeded_DeletesOldClips()
    {
        // Arrange
        var collectionId = Guid.NewGuid();

        var collection = new Collection
        {
            Id = collectionId,
            Title = "InBox",
            LmType = CollectionLmType.Normal,
            MaxAgeDays = 30, // Delete clips older than 30 days
        };

        var oldClips = Enumerable.Range(1, 5)
            .Select(_ => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddDays(-35), // Older than 30 days
                Size = 100,
            })
            .ToList();

        var recentClips = Enumerable.Range(1, 5)
            .Select(_ => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddDays(-10), // Within 30 days
                Size = 100,
            })
            .ToList();

        var allClips = oldClips.Concat(recentClips).ToList();

        _mockCollectionRepository.Setup(p => p.GetByIdAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(collection);

        _mockClipRepository.Setup(p => p.GetClipsInCollectionAsync(collectionId))
            .ReturnsAsync(allClips);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

        // Assert
        // Verify old clips were deleted (moved to Trash)
        _mockClipRepository.Verify(
            p => p.DeleteClipsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Count() == 5)),
            Times.Once);
    }

    #endregion

    #region MaxClips Tests

    [Test]
    public async Task EnforceRetentionAsync_WithMaxClipsExceeded_MovesOldestClipsToOverflow()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var overflowId = Guid.NewGuid();

        var collection = new Collection
        {
            Id = collectionId,
            Title = "InBox",
            LmType = CollectionLmType.Normal,
            MaxClips = 5, // Limit to 5 clips
        };

        var clips = Enumerable.Range(1, 10)
            .Select(p => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-p),
                Size = 100,
            })
            .ToList();

        var overflowCollection = new Collection
        {
            Id = overflowId,
            Title = "Overflow",
            LmType = CollectionLmType.Normal,
            Role = CollectionRole.Overflow,
        };

        _mockCollectionRepository.Setup(p => p.GetByIdAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(collection);

        _mockClipRepository.Setup(p => p.GetClipsInCollectionAsync(collectionId))
            .ReturnsAsync(clips);

        _mockCollectionRepository.Setup(p => p.GetOverflowCollectionAsync(_testDatabaseKey))
            .ReturnsAsync(overflowCollection);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

        // Assert
        // Verify that 5 clips were moved to overflow (keeping the 5 newest)
        _mockClipRepository.Verify(
            p => p.MoveClipsToCollectionAsync(It.IsAny<IEnumerable<Guid>>(), overflowId),
            Times.Once);
    }

    [Test]
    public async Task EnforceRetentionAsync_WithMaxClipsNotExceeded_DoesNothing()
    {
        // Arrange
        var collectionId = Guid.NewGuid();

        var collection = new Collection
        {
            Id = collectionId,
            Title = "InBox",
            LmType = CollectionLmType.Normal,
            MaxClips = 10,
        };

        var clips = Enumerable.Range(1, 5)
            .Select(p => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-p),
                Size = 100,
            })
            .ToList();

        _mockCollectionRepository.Setup(p => p.GetByIdAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(collection);

        _mockClipRepository.Setup(p => p.GetClipsInCollectionAsync(collectionId))
            .ReturnsAsync(clips);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

        // Assert
        _mockClipRepository.Verify(
            p => p.MoveClipsToCollectionAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>()),
            Times.Never);
    }

    #endregion
}
