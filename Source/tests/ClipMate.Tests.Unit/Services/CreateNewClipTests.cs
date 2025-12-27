using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ClipService CreateAsync operation for new clip creation.
/// </summary>
public class CreateNewClipTests : TestFixtureBase
{
    private const string _testDatabaseKey = "db_test0001";
    private readonly Mock<IClipRepository> _mockClipRepository;
    private readonly Mock<IDatabaseContextFactory> _mockContextFactory;
    private readonly Mock<ILogger<ClipService>> _mockLogger;
    private readonly Mock<ISoundService> _mockSoundService;

    public CreateNewClipTests()
    {
        _mockClipRepository = MockRepository.Create<IClipRepository>();
        _mockContextFactory = MockRepository.Create<IDatabaseContextFactory>();
        _mockSoundService = MockRepository.Create<ISoundService>();
        _mockLogger = MockRepository.Create<ILogger<ClipService>>();

        // Setup factory to return mock repository
        _mockContextFactory.Setup(p => p.GetClipRepository(It.IsAny<string>()))
            .Returns(_mockClipRepository.Object);
    }

    private ClipService CreateService() => new(
        _mockContextFactory.Object,
        _mockSoundService.Object,
        Mock.Of<IClipboardService>(),
        Mock.Of<ITemplateService>(),
        _mockLogger.Object);

    #region CreateAsync Tests

    [Test]
    public async Task CreateAsync_WithNewClip_CreatesClipSuccessfully()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var newClip = new Clip
        {
            Title = "New Clip",
            TextContent = string.Empty,
            CollectionId = collectionId,
            CapturedAt = DateTimeOffset.UtcNow,
            Type = ClipType.Text,
        };

        var createdClip = new Clip
        {
            Id = Guid.NewGuid(),
            Title = "New Clip",
            TextContent = string.Empty,
            CollectionId = collectionId,
            CapturedAt = newClip.CapturedAt,
            Type = ClipType.Text,
        };

        // Mock duplicate check (no existing clip)
        _mockClipRepository.Setup(p => p.GetByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        _mockClipRepository.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdClip);

        var service = CreateService();

        // Act
        var result = await service.CreateAsync(_testDatabaseKey, newClip);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Title).IsEqualTo("New Clip");
        await Assert.That(result.CollectionId).IsEqualTo(collectionId);
        _mockClipRepository.Verify(p => p.CreateAsync(newClip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateAsync_WithEmptyTitle_CreatesClipWithEmptyTitle()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var newClip = new Clip
        {
            Title = string.Empty,
            TextContent = string.Empty,
            CollectionId = collectionId,
            CapturedAt = DateTimeOffset.UtcNow,
            Type = ClipType.Text,
        };

        var createdClip = new Clip
        {
            Id = Guid.NewGuid(),
            Title = string.Empty,
            TextContent = string.Empty,
            CollectionId = collectionId,
            CapturedAt = newClip.CapturedAt,
            Type = ClipType.Text,
        };

        // Mock duplicate check (no existing clip)
        _mockClipRepository.Setup(p => p.GetByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        _mockClipRepository.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdClip);

        var service = CreateService();

        // Act
        var result = await service.CreateAsync(_testDatabaseKey, newClip);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Title).IsEqualTo(string.Empty);
        _mockClipRepository.Verify(p => p.CreateAsync(newClip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateAsync_WithTextContent_CreatesClipWithContent()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        const string clipContent = "Test content";
        var newClip = new Clip
        {
            Title = "New Clip",
            TextContent = clipContent,
            CollectionId = collectionId,
            CapturedAt = DateTimeOffset.UtcNow,
            Type = ClipType.Text,
        };

        var createdClip = new Clip
        {
            Id = Guid.NewGuid(),
            Title = "New Clip",
            TextContent = clipContent,
            CollectionId = collectionId,
            CapturedAt = newClip.CapturedAt,
            Type = ClipType.Text,
        };

        // Mock duplicate check (no existing clip)
        _mockClipRepository.Setup(p => p.GetByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        _mockClipRepository.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdClip);

        var service = CreateService();

        // Act
        var result = await service.CreateAsync(_testDatabaseKey, newClip);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.TextContent).IsEqualTo(clipContent);
        _mockClipRepository.Verify(p => p.CreateAsync(newClip, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
