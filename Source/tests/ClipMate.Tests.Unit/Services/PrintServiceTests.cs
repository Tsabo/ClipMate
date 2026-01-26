using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="PrintService" />.
/// </summary>
public class PrintServiceTests
{
    private readonly Mock<IDecryptedBlobCacheService> _cacheService;
    private readonly Mock<IClipService> _clipService;
    private readonly Mock<ILogger<PrintService>> _logger;
    private readonly PrintService _service;

    public PrintServiceTests()
    {
        _clipService = new Mock<IClipService>();
        _cacheService = new Mock<IDecryptedBlobCacheService>();
        _logger = new Mock<ILogger<PrintService>>();
        _service = new PrintService(_clipService.Object, _cacheService.Object, _logger.Object);
    }

    [Test]
    public async Task Constructor_WithNullClipService_ThrowsArgumentNullException()
    {
        // Act & Assert
#pragma warning disable CS8602 // ParamName should always be set for ArgumentNullException
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() => new PrintService(null!, _cacheService.Object, _logger.Object)));
        await Assert.That(ex.ParamName).IsEqualTo("clipService");
#pragma warning restore CS8602
    }

    [Test]
    public async Task Constructor_WithNullCacheService_ThrowsArgumentNullException()
    {
        // Act & Assert
#pragma warning disable CS8602 // ParamName should always be set for ArgumentNullException
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() => new PrintService(_clipService.Object, null!, _logger.Object)));
        await Assert.That(ex.ParamName).IsEqualTo("cacheService");
#pragma warning restore CS8602
    }

    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
#pragma warning disable CS8602 // ParamName should always be set for ArgumentNullException
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() => new PrintService(_clipService.Object, _cacheService.Object, null!)));
        await Assert.That(ex.ParamName).IsEqualTo("logger");
#pragma warning restore CS8602
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithNullClipIds_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.LoadClipDataForPrintingAsync(null!, "test-db"));
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithNullDatabaseKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.LoadClipDataForPrintingAsync([Guid.NewGuid()], null!));
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithEmptyClipIds_ReturnsEmptyList()
    {
        // Arrange
        var clipIds = new List<Guid>();
        const string databaseKey = "test-db";

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithNonExistentClip_SkipsClip()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync((Clip?)null);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithNonEncryptedTextClip_ReturnsTextContent()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        var clip = new Clip
        {
            Id = clipId,
            Title = "Test Clip",
            Creator = "Test Creator",
            CapturedAt = DateTimeOffset.UtcNow,
            SourceUrl = "http://example.com",
            TextContent = "Test content",
            Encrypted = false,
        };

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync(clip);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].ClipId).IsEqualTo(clipId);
        await Assert.That(result[0].Title).IsEqualTo("Test Clip");
        await Assert.That(result[0].Creator).IsEqualTo("Test Creator");
        await Assert.That(result[0].Content).IsEqualTo("Test content");
        await Assert.That(result[0].IsImage).IsFalse();
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithNonEncryptedImageClip_ReturnsImageData()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var clip = new Clip
        {
            Id = clipId,
            Title = "Image Clip",
            Encrypted = false,
            ImageData = imageData,
        };

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync(clip);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].IsImage).IsTrue();
        await Assert.That(result[0].ImageData).IsEqualTo(imageData);
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithEncryptedClip_UsesCache()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        var clip = new Clip
        {
            Id = clipId,
            Title = "Encrypted Clip",
            Encrypted = true,
        };

        var cachedBlobs = new DecryptedBlobData(
            [new BlobTxt { Data = "Decrypted content" }],
            [],
            [],
            []);

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync(clip);
        _cacheService.Setup(p => p.GetDecryptedBlobs(clipId)).Returns(cachedBlobs);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo("Decrypted content");
        _cacheService.Verify(p => p.GetDecryptedBlobs(clipId), Times.Once);
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithEncryptedClipNotInCache_ReturnsNotAvailable()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        var clip = new Clip
        {
            Id = clipId,
            Title = "Encrypted Clip",
            Encrypted = true,
        };

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync(clip);
        _cacheService.Setup(p => p.GetDecryptedBlobs(clipId)).Returns((DecryptedBlobData?)null);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo("[Encrypted - Not Available]");
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithEncryptedImageInCache_ReturnsImageData()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF }; // JPG header
        var clip = new Clip
        {
            Id = clipId,
            Title = "Encrypted Image",
            Encrypted = true,
        };

        var cachedBlobs = new DecryptedBlobData(
            [],
            [new BlobJpg { Data = imageData }],
            [],
            []);

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync(clip);
        _cacheService.Setup(p => p.GetDecryptedBlobs(clipId)).Returns(cachedBlobs);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].IsImage).IsTrue();
        await Assert.That(result[0].ImageData).IsEqualTo(imageData);
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithEncryptedPngInCache_ReturnsImageData()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var clip = new Clip
        {
            Id = clipId,
            Title = "Encrypted PNG",
            Encrypted = true,
        };

        var cachedBlobs = new DecryptedBlobData(
            [],
            [],
            [new BlobPng { Data = imageData }],
            []);

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync(clip);
        _cacheService.Setup(p => p.GetDecryptedBlobs(clipId)).Returns(cachedBlobs);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].IsImage).IsTrue();
        await Assert.That(result[0].ImageData).IsEqualTo(imageData);
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithMultipleClips_ReturnsAllClips()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId1, clipId2 };
        const string databaseKey = "test-db";
        var clip1 = new Clip
        {
            Id = clipId1,
            Title = "Clip 1",
            TextContent = "Content 1",
            Encrypted = false,
        };

        var clip2 = new Clip
        {
            Id = clipId2,
            Title = "Clip 2",
            TextContent = "Content 2",
            Encrypted = false,
        };

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId1)).ReturnsAsync(clip1);
        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId2)).ReturnsAsync(clip2);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Title).IsEqualTo("Clip 1");
        await Assert.That(result[1].Title).IsEqualTo("Clip 2");
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithMixedExistingAndMissingClips_ReturnsOnlyExisting()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var clipId3 = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId1, clipId2, clipId3 };
        const string databaseKey = "test-db";
        var clip1 = new Clip
        {
            Id = clipId1,
            Title = "Clip 1",
            TextContent = "Content 1",
            Encrypted = false,
        };

        var clip3 = new Clip
        {
            Id = clipId3,
            Title = "Clip 3",
            TextContent = "Content 3",
            Encrypted = false,
        };

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId1)).ReturnsAsync(clip1);
        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId2)).ReturnsAsync((Clip?)null);
        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId3)).ReturnsAsync(clip3);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].ClipId).IsEqualTo(clipId1);
        await Assert.That(result[1].ClipId).IsEqualTo(clipId3);
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_WithNonEncryptedClipNoContent_ReturnsNoTextContent()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        var clip = new Clip
        {
            Id = clipId,
            Title = "Empty Clip",
            TextContent = null,
            Encrypted = false,
        };

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync(clip);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo("[No Text Content]");
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_CallsLoadBlobDataForNonEncryptedClips()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        var clip = new Clip
        {
            Id = clipId,
            Title = "Test Clip",
            TextContent = "Content",
            Encrypted = false,
        };

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync(clip);

        // Act
        await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        _clipService.Verify(p => p.LoadBlobDataAsync(databaseKey, clip), Times.Once);
    }

    [Test]
    public async Task LoadClipDataForPrintingAsync_PreservesAllClipMetadata()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId };
        const string databaseKey = "test-db";
        var capturedAt = DateTimeOffset.Parse("2026-01-25T10:30:00Z");
        var clip = new Clip
        {
            Id = clipId,
            Title = "Important Clip",
            Creator = "John Doe",
            CapturedAt = capturedAt,
            SourceUrl = "https://example.com/source",
            TextContent = "Important content",
            Encrypted = false,
        };

        _clipService.Setup(p => p.GetByIdAsync(databaseKey, clipId)).ReturnsAsync(clip);

        // Act
        var result = await _service.LoadClipDataForPrintingAsync(clipIds, databaseKey);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Title).IsEqualTo("Important Clip");
        await Assert.That(result[0].Creator).IsEqualTo("John Doe");
        await Assert.That(result[0].Created).IsEqualTo(capturedAt.DateTime);
        await Assert.That(result[0].Url).IsEqualTo("https://example.com/source");
    }
}
