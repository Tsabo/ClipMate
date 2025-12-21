using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ClipService extended operations: Rename, Copy, Move.
/// Test-driven development for new clip management features.
/// </summary>
public class ClipServiceExtendedTests : TestFixtureBase
{
    private const string _testDatabaseKey = "db_test0001";
    private readonly ILogger<ClipService> _logger;
    private readonly Mock<IBlobRepository> _mockBlobRepository;
    private readonly Mock<IClipDataRepository> _mockClipDataRepository;
    private readonly Mock<IClipRepository> _mockClipRepository;
    private readonly Mock<IDatabaseContextFactory> _mockDatabaseContextFactory;
    private readonly Mock<IDatabaseManager> _mockDatabaseManager;
    private readonly Mock<IClipRepositoryFactory> _mockRepositoryFactory;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<ISoundService> _mockSoundService;

    public ClipServiceExtendedTests()
    {
        _mockClipRepository = MockRepository.Create<IClipRepository>();
        // Use loose behavior for ClipData and Blob repositories since not all tests need them
        _mockClipDataRepository = new Mock<IClipDataRepository>(MockBehavior.Loose);
        _mockBlobRepository = new Mock<IBlobRepository>(MockBehavior.Loose);
        _mockRepositoryFactory = MockRepository.Create<IClipRepositoryFactory>();
        _mockDatabaseContextFactory = MockRepository.Create<IDatabaseContextFactory>();
        _mockDatabaseManager = MockRepository.Create<IDatabaseManager>();
        _mockSoundService = MockRepository.Create<ISoundService>();
        _logger = CreateLogger<ClipService>();

        // Setup scoped service provider for ClipData and Blob repositories
        _mockServiceScope = new Mock<IServiceScope>(MockBehavior.Loose);
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Loose);
        _mockServiceProvider = new Mock<IServiceProvider>(MockBehavior.Loose);

        var scopeServiceProvider = new Mock<IServiceProvider>(MockBehavior.Loose);
        scopeServiceProvider.Setup(p => p.GetService(typeof(IClipDataRepository)))
            .Returns(_mockClipDataRepository.Object);

        scopeServiceProvider.Setup(p => p.GetService(typeof(IBlobRepository)))
            .Returns(_mockBlobRepository.Object);

        _mockServiceScope.Setup(p => p.ServiceProvider).Returns(scopeServiceProvider.Object);
        _mockServiceScopeFactory.Setup(p => p.CreateScope()).Returns(_mockServiceScope.Object);

        // Setup main service provider to return the scope factory
        _mockServiceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockServiceScopeFactory.Object);

        // Setup factory to return mock repository
        _mockRepositoryFactory.Setup(p => p.CreateRepository(It.IsAny<string>()))
            .Returns(_mockClipRepository.Object);

        // Setup DatabaseContextFactory to return mock repositories
        _mockDatabaseContextFactory.Setup(p => p.GetClipDataRepository(It.IsAny<ClipMateDbContext>()))
            .Returns(_mockClipDataRepository.Object);

        _mockDatabaseContextFactory.Setup(p => p.GetBlobRepository(It.IsAny<ClipMateDbContext>()))
            .Returns(_mockBlobRepository.Object);
    }

    private ClipService CreateService() => new(
        _mockRepositoryFactory.Object,
        _mockDatabaseContextFactory.Object,
        _mockDatabaseManager.Object,
        _mockSoundService.Object,
        _logger);

    #region RenameClipAsync Tests

    [Test]
    public async Task RenameClipAsync_WithValidIdAndTitle_UpdatesClipTitle()
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

        _mockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        _mockClipRepository.Setup(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act
        var result = await service.RenameClipAsync(_testDatabaseKey, clipId, "New Title");

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.Title).IsEqualTo("New Title");
        VerifyAll();
    }

    [Test]
    public async Task RenameClipAsync_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        _mockClipRepository.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        var service = CreateService();

        // Act
        var result = await service.RenameClipAsync(_testDatabaseKey, Guid.NewGuid(), "New Title");

        // Assert
        await Assert.That(result).IsFalse();
        VerifyAll();
    }

    [Test]
    public async Task RenameClipAsync_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.RenameClipAsync(_testDatabaseKey, Guid.NewGuid(), string.Empty))
            .Throws<ArgumentException>();
    }

    #endregion

    #region CopyClipAsync Tests

    [Test]
    public async Task CopyClipAsync_WithValidId_CreatesNewClip()
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

        _mockClipRepository.Setup(p => p.GetByIdAsync(sourceClipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceClip);

        _mockClipRepository.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newClip);

        // Setup ClipDataRepository to return empty list (no formats to copy)
        _mockClipDataRepository.Setup(p => p.GetByClipIdAsync(sourceClipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClipData>());

        var service = CreateService();

        // Act
        var result = await service.CopyClipAsync(_testDatabaseKey, sourceClipId, targetCollectionId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsNotEqualTo(sourceClipId);
        await Assert.That(result.CollectionId).IsEqualTo(targetCollectionId);
        VerifyAll();
    }

    [Test]
    public async Task CopyClipAsync_WithNonExistentId_ThrowsArgumentException()
    {
        // Arrange
        _mockClipRepository.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.CopyClipAsync(_testDatabaseKey, Guid.NewGuid(), Guid.NewGuid()))
            .Throws<ArgumentException>();

        VerifyAll();
    }

    #endregion

    #region MoveClipAsync Tests

    [Test]
    public async Task MoveClipAsync_WithValidId_UpdatesCollectionId()
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

        _mockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        _mockClipRepository.Setup(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act
        var result = await service.MoveClipAsync(_testDatabaseKey, clipId, newCollectionId);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.CollectionId).IsEqualTo(newCollectionId);
        VerifyAll();
    }

    [Test]
    public async Task MoveClipAsync_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        _mockClipRepository.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        var service = CreateService();

        // Act
        var result = await service.MoveClipAsync(_testDatabaseKey, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        await Assert.That(result).IsFalse();
        VerifyAll();
    }

    [Test]
    public async Task MoveClipAsync_ToSameCollection_ReturnsTrueWithoutUpdate()
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

        _mockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        var service = CreateService();

        // Act
        var result = await service.MoveClipAsync(_testDatabaseKey, clipId, collectionId);

        // Assert - should return true but not call UpdateAsync since already in target collection
        await Assert.That(result).IsTrue();
        _mockClipRepository.Verify(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyAll();
    }

    #endregion
}
