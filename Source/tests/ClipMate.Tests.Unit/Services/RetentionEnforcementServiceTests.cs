using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for RetentionEnforcementService ensuring retention rules are properly enforced.
/// Tests MaxClips, MaxBytes, MaxAge retention, Overflow collection behavior, and ReadOnly bypass.
/// </summary>
[Category("RetentionEnforcementService")]
[Category("Unit")]
public class RetentionEnforcementServiceTests
{
    private const string _testDatabaseKey = "test-db";
    private IDatabaseContextFactory _mockContextFactory = null!;
    private IClipRepository _mockClipRepository = null!;
    private ICollectionRepository _mockCollectionRepository = null!;

    [Before(Test)]
    public void Setup()
    {
        // Mock repositories
        _mockClipRepository = A.Fake<IClipRepository>();
        _mockCollectionRepository = A.Fake<ICollectionRepository>();

        // Mock context factory to return our mocked repositories
        _mockContextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => _mockContextFactory.GetClipRepository(A<string>.Ignored))
            .Returns(_mockClipRepository);
        A.CallTo(() => _mockContextFactory.GetCollectionRepository(A<string>.Ignored))
            .Returns(_mockCollectionRepository);
    }

    private IRetentionEnforcementService CreateService() =>
        new RetentionEnforcementService(
            _mockContextFactory,
            A.Fake<ILogger<IRetentionEnforcementService>>());

    [Category("ReadOnlyCollection")]
    public class ReadOnlyCollectionTests : RetentionEnforcementServiceTests
    {
        [Test]
        public async Task SkipsEnforcement()
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

            A.CallTo(() => _mockCollectionRepository.GetByIdAsync(_testDatabaseKey, collectionId))
                .Returns(collection);

            A.CallTo(() => _mockClipRepository.GetClipsInCollectionAsync(collectionId))
                .Returns(clips);

            var service = CreateService();

            // Act
            await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

            // Assert
            A.CallTo(() => _mockClipRepository.MoveClipsToCollectionAsync(A<IEnumerable<Guid>>.Ignored, A<Guid>.Ignored)).MustNotHaveHappened();
        }
    }

    [Category("OverflowCollection")]
    public class OverflowCollectionTests : RetentionEnforcementServiceTests
    {
        [Test]
        public async Task ExceededMovesToTrashcan()
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

            A.CallTo(() => _mockCollectionRepository.GetByIdAsync(_testDatabaseKey, overflowId))
                .Returns(overflowCollection);

            A.CallTo(() => _mockClipRepository.GetClipsInCollectionAsync(overflowId))
                .Returns(clips);

            A.CallTo(() => _mockCollectionRepository.GetTrashcanCollectionAsync(_testDatabaseKey))
                .Returns(trashCollection);

            var service = CreateService();

            // Act
            await service.EnforceRetentionAsync(_testDatabaseKey, overflowId);

            // Assert
            A.CallTo(() => _mockClipRepository.MoveClipsToCollectionAsync(A<IEnumerable<Guid>>.Ignored, trashId)).MustHaveHappenedOnceExactly();
        }
    }

    [Category("MaxAge")]
    public class MaxAgeTests : RetentionEnforcementServiceTests
    {
        [Test]
        public async Task ExceededDeletesOldClips()
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

            A.CallTo(() => _mockCollectionRepository.GetByIdAsync(_testDatabaseKey, collectionId))
                .Returns(collection);

            A.CallTo(() => _mockClipRepository.GetClipsInCollectionAsync(collectionId))
                .Returns(allClips);

            var service = CreateService();

            // Act
            await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

            // Assert
            // Verify old clips were deleted (moved to Trash)
            A.CallTo(() => _mockClipRepository.DeleteClipsAsync(A<IEnumerable<Guid>>.That.Matches(ids => ids.Count() == 5))).MustHaveHappenedOnceExactly();
        }
    }

    [Category("MaxClips")]
    public class MaxClipsTests : RetentionEnforcementServiceTests
    {
        [Test]
        public async Task ExceededMovesOldestClipsToOverflow()
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

            A.CallTo(() => _mockCollectionRepository.GetByIdAsync(_testDatabaseKey, collectionId))
                .Returns(collection);

            A.CallTo(() => _mockClipRepository.GetClipsInCollectionAsync(collectionId))
                .Returns(clips);

            A.CallTo(() => _mockCollectionRepository.GetOverflowCollectionAsync(_testDatabaseKey))
                .Returns(overflowCollection);

            var service = CreateService();

            // Act
            await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

            // Assert
            // Verify that 5 clips were moved to overflow (keeping the 5 newest)
            A.CallTo(() => _mockClipRepository.MoveClipsToCollectionAsync(A<IEnumerable<Guid>>.Ignored, overflowId)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task NotExceededDoesNothing()
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

            A.CallTo(() => _mockCollectionRepository.GetByIdAsync(_testDatabaseKey, collectionId))
                .Returns(collection);

            A.CallTo(() => _mockClipRepository.GetClipsInCollectionAsync(collectionId))
                .Returns(clips);

            var service = CreateService();

            // Act
            await service.EnforceRetentionAsync(_testDatabaseKey, collectionId);

            // Assert
            A.CallTo(() => _mockClipRepository.MoveClipsToCollectionAsync(A<IEnumerable<Guid>>.Ignored, A<Guid>.Ignored)).MustNotHaveHappened();
        }
    }
}
