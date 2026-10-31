using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ClipService CreateAsync operation for new clip creation.
/// </summary>
[Category("ClipService")]
[Category("Unit")]
public class CreateNewClipTests : TestFixtureBase
{
    private const string _testDatabaseKey = "db_test0001";
    private readonly IClipRepository _mockClipRepository;
    private readonly IDatabaseContextFactory _mockContextFactory;
    private readonly ILogger<ClipService> _mockLogger;

    public CreateNewClipTests()
    {
        _mockClipRepository = A.Fake<IClipRepository>(o => o.Strict());
        _mockContextFactory = A.Fake<IDatabaseContextFactory>(o => o.Strict());
        _mockLogger = A.Fake<ILogger<ClipService>>(o => o.Strict());

        // Setup factory to return mock repository
        A.CallTo(() => _mockContextFactory.GetClipRepository(A<string>.Ignored))
            .Returns(_mockClipRepository);
    }

    private ClipService CreateService() => new(
        _mockContextFactory,
        A.Fake<IConfigurationService>(),
        A.Fake<IClipboardService>(),
        A.Fake<ITemplateService>(),
        A.Fake<IEncryptionService>(),
        A.Fake<IDecryptedBlobCacheService>(),
        A.Fake<IClipContentService>(),
        A.Fake<IMessenger>(),
        _mockLogger);

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
        A.CallTo(() => _mockClipRepository.GetByContentHashAsync(A<string>.Ignored, A<CancellationToken>.Ignored))
            .Returns((Clip?)null);

        A.CallTo(() => _mockClipRepository.CreateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored))
            .Returns(createdClip);

        var service = CreateService();

        // Act
        var result = await service.CreateAsync(_testDatabaseKey, newClip);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Title).IsEqualTo("New Clip");
        await Assert.That(result.CollectionId).IsEqualTo(collectionId);
        A.CallTo(() => _mockClipRepository.CreateAsync(newClip, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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
        A.CallTo(() => _mockClipRepository.GetByContentHashAsync(A<string>.Ignored, A<CancellationToken>.Ignored))
            .Returns((Clip?)null);

        A.CallTo(() => _mockClipRepository.CreateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored))
            .Returns(createdClip);

        var service = CreateService();

        // Act
        var result = await service.CreateAsync(_testDatabaseKey, newClip);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Title).IsEqualTo(string.Empty);
        A.CallTo(() => _mockClipRepository.CreateAsync(newClip, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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
        A.CallTo(() => _mockClipRepository.GetByContentHashAsync(A<string>.Ignored, A<CancellationToken>.Ignored))
            .Returns((Clip?)null);

        A.CallTo(() => _mockClipRepository.CreateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored))
            .Returns(createdClip);

        var service = CreateService();

        // Act
        var result = await service.CreateAsync(_testDatabaseKey, newClip);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.TextContent).IsEqualTo(clipContent);
        A.CallTo(() => _mockClipRepository.CreateAsync(newClip, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }
}
