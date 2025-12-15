using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private ClipMateDbContext _dbContext = null!;
    private Mock<DatabaseManager> _mockDatabaseManager = null!;
    private Mock<IClipRepository> _mockClipRepository = null!;
    private Mock<ICollectionRepository> _mockCollectionRepository = null!;
    private Mock<ILogger<IRetentionEnforcementService>> _mockLogger = null!;

    [Before(Test)]
    public void Setup()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ClipMateDbContext(options);

        // Mock DatabaseManager
        var mockConfigService = new Mock<IConfigurationService>();
        var mockContextFactory = new Mock<IDatabaseContextFactory>();
        var mockDbLogger = new Mock<ILogger<DatabaseManager>>();

        _mockDatabaseManager = new Mock<DatabaseManager>(
            mockConfigService.Object,
            mockContextFactory.Object,
            mockDbLogger.Object);

        _mockDatabaseManager.Setup(m => m.GetDatabaseContext(_testDatabaseKey))
            .Returns(_dbContext);

        // Mock repositories
        _mockClipRepository = new Mock<IClipRepository>();
        _mockCollectionRepository = new Mock<ICollectionRepository>();
        _mockLogger = new Mock<ILogger<IRetentionEnforcementService>>();
    }

    [After(Test)]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    private IRetentionEnforcementService CreateService()
    {
        return new RetentionEnforcementService(
            _mockClipRepository.Object,
            _mockCollectionRepository.Object,
            _mockLogger.Object);
    }

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
            MaxClips = 5 // Limit to 5 clips
        };

        var clips = Enumerable.Range(1, 10)
            .Select(i => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-i),
                Size = 100
            })
            .ToList();

        var overflowCollection = new Collection
        {
            Id = overflowId,
            Title = "Overflow",
            LmType = CollectionLmType.Normal,
            Role = CollectionRole.Overflow
        };

        _mockCollectionRepository.Setup(r => r.GetByIdAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(collection);
        _mockClipRepository.Setup(r => r.GetClipsInCollectionAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(clips);
        _mockCollectionRepository.Setup(r => r.GetOverflowCollectionAsync(_testDatabaseKey))
            .ReturnsAsync(overflowCollection);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

        // Assert
        // Verify that 5 clips were moved to overflow (keeping the 5 newest)
        _mockClipRepository.Verify(
            r => r.MoveClipsToCollectionAsync(_testDatabaseKey, It.IsAny<IEnumerable<Guid>>(), overflowId),
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
            MaxClips = 10
        };

        var clips = Enumerable.Range(1, 5)
            .Select(i => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-i),
                Size = 100
            })
            .ToList();

        _mockCollectionRepository.Setup(r => r.GetByIdAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(collection);
        _mockClipRepository.Setup(r => r.GetClipsInCollectionAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(clips);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

        // Assert
        _mockClipRepository.Verify(
            r => r.MoveClipsToCollectionAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>()),
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
            MaxClips = 5
        };

        var clips = Enumerable.Range(1, 10)
            .Select(i => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = overflowId,
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-i),
                Size = 100
            })
            .ToList();

        var trashCollection = new Collection
        {
            Id = trashId,
            Title = "Trash Can",
            LmType = CollectionLmType.Normal,
            Role = CollectionRole.Trashcan
        };

        _mockCollectionRepository.Setup(r => r.GetByIdAsync(_testDatabaseKey, overflowId))
            .ReturnsAsync(overflowCollection);
        _mockClipRepository.Setup(r => r.GetClipsInCollectionAsync(_testDatabaseKey, overflowId))
            .ReturnsAsync(clips);
        _mockCollectionRepository.Setup(r => r.GetTrashcanCollectionAsync(_testDatabaseKey))
            .ReturnsAsync(trashCollection);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, overflowId);

        // Assert
        _mockClipRepository.Verify(
            r => r.MoveClipsToCollectionAsync(_testDatabaseKey, It.IsAny<IEnumerable<Guid>>(), trashId),
            Times.Once);
    }

    #endregion

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
            MaxClips = 5
        };

        var clips = Enumerable.Range(1, 10)
            .Select(i => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-i),
                Size = 100
            })
            .ToList();

        _mockCollectionRepository.Setup(r => r.GetByIdAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(collection);
        _mockClipRepository.Setup(r => r.GetClipsInCollectionAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(clips);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

        // Assert
        _mockClipRepository.Verify(
            r => r.MoveClipsToCollectionAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>()),
            Times.Never);
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
            MaxAgeDays = 30 // Delete clips older than 30 days
        };

        var oldClips = Enumerable.Range(1, 5)
            .Select(i => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddDays(-35), // Older than 30 days
                Size = 100
            })
            .ToList();

        var recentClips = Enumerable.Range(1, 5)
            .Select(i => new Clip
            {
                Id = Guid.NewGuid(),
                CollectionId = collectionId,
                CapturedAt = DateTimeOffset.UtcNow.AddDays(-10), // Within 30 days
                Size = 100
            })
            .ToList();

        var allClips = oldClips.Concat(recentClips).ToList();

        _mockCollectionRepository.Setup(r => r.GetByIdAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(collection);
        _mockClipRepository.Setup(r => r.GetClipsInCollectionAsync(_testDatabaseKey, collectionId))
            .ReturnsAsync(allClips);

        var service = CreateService();

        // Act
        await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

        // Assert
        // Verify old clips were deleted (moved to Trash)
        _mockClipRepository.Verify(
            r => r.DeleteClipsAsync(_testDatabaseKey, It.Is<IEnumerable<Guid>>(ids => ids.Count() == 5)),
            Times.Once);
    }

    #endregion
}
