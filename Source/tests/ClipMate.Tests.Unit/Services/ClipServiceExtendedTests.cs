using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ClipService extended operations: Rename, Copy, Move.
/// Test-driven development for new clip management features.
/// </summary>
[Category("ClipService")]
[Category("Unit")]
public class ClipServiceExtendedTests : TestFixtureBase
{
    private const string _testDatabaseKey = "db_test0001";
    private readonly ILogger<ClipService> _logger;
    private readonly IClipDataRepository _mockClipDataRepository;
    private readonly IClipRepository _mockClipRepository;
    private readonly IDatabaseContextFactory _mockDatabaseContextFactory;
    private readonly IDatabaseManager _mockDatabaseManager;

    public ClipServiceExtendedTests()
    {
        // Use loose mocks since not all methods require all dependencies to be called
        _mockClipRepository = A.Fake<IClipRepository>();
        _mockClipDataRepository = A.Fake<IClipDataRepository>();
        var mockBlobRepository = A.Fake<IBlobRepository>();
        _mockDatabaseContextFactory = A.Fake<IDatabaseContextFactory>();
        _mockDatabaseManager = A.Fake<IDatabaseManager>();
        _logger = CreateLogger<ClipService>();

        // Setup scoped service provider for ClipData and Blob repositories
        var mockServiceScope = A.Fake<IServiceScope>();
        var mockServiceScopeFactory = A.Fake<IServiceScopeFactory>();
        var mockServiceProvider = A.Fake<IServiceProvider>();

        var scopeServiceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => scopeServiceProvider.GetService(typeof(IClipDataRepository)))
            .Returns(_mockClipDataRepository);

        A.CallTo(() => scopeServiceProvider.GetService(typeof(IBlobRepository)))
            .Returns(mockBlobRepository);

        A.CallTo(() => mockServiceScope.ServiceProvider).Returns(scopeServiceProvider);
        A.CallTo(() => mockServiceScopeFactory.CreateScope()).Returns(mockServiceScope);

        // Setup main service provider to return the scope factory
        A.CallTo(() => mockServiceProvider.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockServiceScopeFactory);

        // Setup factory to return mock repository
        A.CallTo(() => _mockDatabaseContextFactory.GetClipRepository(A<string>.Ignored))
            .Returns(_mockClipRepository);

        // Setup DatabaseContextFactory to return mock repositories
        A.CallTo(() => _mockDatabaseContextFactory.GetClipDataRepository(A<string>.Ignored))
            .Returns(_mockClipDataRepository);

        A.CallTo(() => _mockDatabaseContextFactory.GetBlobRepository(A<string>.Ignored))
            .Returns(mockBlobRepository);
    }

    private ClipService CreateService() => new(
        _mockDatabaseContextFactory,
        new ClipStore(_mockDatabaseContextFactory, NullLogger<ClipStore>.Instance),
        A.Fake<IConfigurationService>(),
        A.Fake<IEncryptionService>(),
        A.Fake<IDecryptedBlobCacheService>(),
        A.Fake<IDatabases>(),
        A.Fake<IMessenger>(),
        _logger);

    [Category("RenameClip")]
    public class RenameClipAsyncTests : ClipServiceExtendedTests
    {
        [Test]
        public async Task WithValidIdAndTitle_UpdatesClipTitle()
        {
            // Arrange
            var clipId = Guid.NewGuid();
            var clip = new Clip
            {
                Id = clipId,
                Title = "Old Title",
                TextContent = "Content",
                CapturedAt = DateTime.UtcNow,
            };

            A.CallTo(() => _mockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
                .Returns(clip);

            A.CallTo(() => _mockClipRepository.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored))
                .Returns(true);

            var service = CreateService();

            // Act
            var result = await service.RenameClipAsync(_testDatabaseKey, clipId, "New Title");

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(clip.Title).IsEqualTo("New Title");
        }

        [Test]
        public async Task WithNonExistentId_ReturnsFalse()
        {
            // Arrange
            A.CallTo(() => _mockClipRepository.GetByIdAsync(A<Guid>.Ignored, A<CancellationToken>.Ignored))
                .Returns((Clip?)null);

            var service = CreateService();

            // Act
            var result = await service.RenameClipAsync(_testDatabaseKey, Guid.NewGuid(), "New Title");

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task WithEmptyTitle_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            await Assert.That(async () => await service.RenameClipAsync(_testDatabaseKey, Guid.NewGuid(), string.Empty))
                .Throws<ArgumentException>();
        }
    }

    [Category("CopyClip")]
    public class CopyClipAsyncTests : ClipServiceExtendedTests
    {
        [Test]
        public async Task WithValidId_CreatesNewClip()
        {
            // Arrange
            var sourceClipId = Guid.NewGuid();
            var targetCollectionId = Guid.NewGuid();
            var sourceClip = new Clip
            {
                Id = sourceClipId,
                Title = "Source Title",
                TextContent = "Content to copy",
                CollectionId = Guid.NewGuid(),
                CapturedAt = DateTime.UtcNow,
            };

            var newClip = new Clip
            {
                Id = Guid.NewGuid(),
                Title = "Source Title (Copy)",
                TextContent = "Content to copy",
                CollectionId = targetCollectionId,
                CapturedAt = DateTime.UtcNow,
            };

            // Create real SQLite in-memory database context for CopyClipDataAndBlobsAsync
            var options = new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;

            using var dbContext = new ClipMateDbContext(options);
            dbContext.Database.OpenConnection();
            dbContext.Database.EnsureCreated();

            A.CallTo(() => _mockDatabaseManager.CreateDatabaseContext(_testDatabaseKey))
                .Returns(dbContext);

            A.CallTo(() => _mockClipRepository.GetByIdAsync(sourceClipId, A<CancellationToken>.Ignored))
                .Returns(sourceClip);

            A.CallTo(() => _mockClipRepository.CreateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored))
                .Returns(newClip);

            // Setup ClipDataRepository to return empty list (no formats to copy)
            A.CallTo(() => _mockClipDataRepository.GetByClipIdAsync(sourceClipId, A<CancellationToken>.Ignored))
                .Returns(new List<ClipData>());

            var service = CreateService();

            // Act
            var result = await service.CopyClipAsync(_testDatabaseKey, sourceClipId, targetCollectionId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Id).IsNotEqualTo(sourceClipId);
            await Assert.That(result.CollectionId).IsEqualTo(targetCollectionId);
        }

        [Test]
        public async Task WithNonExistentId_ThrowsArgumentException()
        {
            // Arrange
            A.CallTo(() => _mockClipRepository.GetByIdAsync(A<Guid>.Ignored, A<CancellationToken>.Ignored))
                .Returns((Clip?)null);

            var service = CreateService();

            // Act & Assert
            await Assert.That(async () => await service.CopyClipAsync(_testDatabaseKey, Guid.NewGuid(), Guid.NewGuid()))
                .Throws<ArgumentException>();

        }
    }

    [Category("MoveClip")]
    public class MoveClipAsyncTests : ClipServiceExtendedTests
    {
        [Test]
        public async Task WithValidId_UpdatesCollectionId()
        {
            // Arrange
            var clipId = Guid.NewGuid();
            var oldCollectionId = Guid.NewGuid();
            var newCollectionId = Guid.NewGuid();

            var clip = new Clip
            {
                Id = clipId,
                Title = "Test Clip",
                TextContent = "Content",
                CollectionId = oldCollectionId,
                CapturedAt = DateTime.UtcNow,
            };

            A.CallTo(() => _mockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
                .Returns(clip);

            A.CallTo(() => _mockClipRepository.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored))
                .Returns(true);

            var service = CreateService();

            // Act
            var result = await service.MoveClipAsync(_testDatabaseKey, clipId, newCollectionId);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(clip.CollectionId).IsEqualTo(newCollectionId);
        }

        [Test]
        public async Task WithNonExistentId_ReturnsFalse()
        {
            // Arrange
            A.CallTo(() => _mockClipRepository.GetByIdAsync(A<Guid>.Ignored, A<CancellationToken>.Ignored))
                .Returns((Clip?)null);

            var service = CreateService();

            // Act
            var result = await service.MoveClipAsync(_testDatabaseKey, Guid.NewGuid(), Guid.NewGuid());

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task ToSameCollection_ReturnsTrueWithoutUpdate()
        {
            // Arrange
            var clipId = Guid.NewGuid();
            var collectionId = Guid.NewGuid();

            var clip = new Clip
            {
                Id = clipId,
                Title = "Test Clip",
                CollectionId = collectionId,
                CapturedAt = DateTime.UtcNow,
            };

            A.CallTo(() => _mockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
                .Returns(clip);

            var service = CreateService();

            // Act
            var result = await service.MoveClipAsync(_testDatabaseKey, clipId, collectionId);

            // Assert - should return true but not call UpdateAsync since already in target collection
            await Assert.That(result).IsTrue();
            A.CallTo(() => _mockClipRepository.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        }
    }
}
