using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="DecryptedBlobCacheService" />.
/// </summary>
public class DecryptedBlobCacheServiceTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<DecryptedBlobCacheService>> _logger;
    private readonly DecryptedBlobCacheService _service;

    public DecryptedBlobCacheServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = new Mock<ILogger<DecryptedBlobCacheService>>();
        _service = new DecryptedBlobCacheService(_cache, _logger.Object);
    }

    public void Dispose()
    {
        _service?.Dispose();
        _cache?.Dispose();
    }

    [Test]
    public async Task CacheDecryptedBlobs_WithDefaultExpiration_CachesBlobsSuccessfully()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs = CreateTestBlobData();

        // Act
        _service.CacheDecryptedBlobs(clipId, blobs);

        // Assert
        var cached = _service.GetDecryptedBlobs(clipId);
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.TextBlobs.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetDecryptedBlobs_WhenNotCached_ReturnsNull()
    {
        // Arrange
        var clipId = Guid.NewGuid();

        // Act
        var result = _service.GetDecryptedBlobs(clipId);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task IsClipDecrypted_WhenCached_ReturnsTrue()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs = CreateTestBlobData();
        _service.CacheDecryptedBlobs(clipId, blobs);

        // Act
        var result = _service.IsClipDecrypted(clipId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsClipDecrypted_WhenNotCached_ReturnsFalse()
    {
        // Arrange
        var clipId = Guid.NewGuid();

        // Act
        var result = _service.IsClipDecrypted(clipId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ClearClip_RemovesCachedBlobs()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs = CreateTestBlobData();
        _service.CacheDecryptedBlobs(clipId, blobs);

        // Act
        _service.ClearClip(clipId);

        // Assert
        var result = _service.GetDecryptedBlobs(clipId);
        await Assert.That(result).IsNull();
        await Assert.That(_service.IsClipDecrypted(clipId)).IsFalse();
    }

    [Test]
    public async Task GetAllCachedClipIds_ReturnsAllCachedClips()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var blobs = CreateTestBlobData();
        _service.CacheDecryptedBlobs(clipId1, blobs);
        _service.CacheDecryptedBlobs(clipId2, blobs);

        // Act
        var cachedIds = _service.GetAllCachedClipIds();

        // Assert
        await Assert.That(cachedIds.Count).IsEqualTo(2);
        await Assert.That(cachedIds.Contains(clipId1)).IsTrue();
        await Assert.That(cachedIds.Contains(clipId2)).IsTrue();
    }

    [Test]
    public async Task GetAllCachedClipIds_WhenEmpty_ReturnsEmptyList()
    {
        // Act
        var cachedIds = _service.GetAllCachedClipIds();

        // Assert
        await Assert.That(cachedIds.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ClearAll_RemovesAllCachedBlobs()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var blobs = CreateTestBlobData();
        _service.CacheDecryptedBlobs(clipId1, blobs);
        _service.CacheDecryptedBlobs(clipId2, blobs);

        // Act
        _service.ClearAll();

        // Assert - Note: Cache entries may still exist briefly, but timers are cancelled
        var cachedIds = _service.GetAllCachedClipIds();
        await Assert.That(cachedIds.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CacheDecryptedBlobs_WithMaxValueExpiration_NeverRemovesPriority()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs = CreateTestBlobData();

        // Act
        _service.CacheDecryptedBlobs(clipId, blobs, TimeSpan.MaxValue);

        // Assert
        var cached = _service.GetDecryptedBlobs(clipId);
        await Assert.That(cached).IsNotNull();

        // Verify logging for "until shutdown"
        _logger.Verify(
            p => p.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("until shutdown")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task CacheDecryptedBlobs_ReplacingExistingEntry_UpdatesCachedData()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs1 = CreateTestBlobData();
        var blobs2 = new DecryptedBlobData(
            [new BlobTxt { Id = Guid.NewGuid(), Data = "Replaced" }],
            [],
            [],
            []);

        // Act - Cache same clipId twice with different data
        _service.CacheDecryptedBlobs(clipId, blobs1, TimeSpan.FromSeconds(10));
        _service.CacheDecryptedBlobs(clipId, blobs2, TimeSpan.FromSeconds(10));

        // Assert - Should have replaced the entry with new data
        var cached = _service.GetDecryptedBlobs(clipId);
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.TextBlobs[0].Data).IsEqualTo("Replaced");
        await Assert.That(_service.IsClipDecrypted(clipId)).IsTrue();
    }

    [Test]
    public async Task CacheDecryptedBlobs_WithExpirationCallback_InvokesCallbackAfterExpiry()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs = CreateTestBlobData();
        Guid? expiredClipId = null;
        var callbackInvoked = new TaskCompletionSource<bool>();

        // Act
        _service.CacheDecryptedBlobs(
            clipId,
            blobs,
            TimeSpan.FromMilliseconds(100),
            id =>
            {
                expiredClipId = id;
                callbackInvoked.SetResult(true);
            });

        // Wait for callback
        var timedOut = await Task.WhenAny(callbackInvoked.Task, Task.Delay(2000)) != callbackInvoked.Task;

        // Assert
        await Assert.That(timedOut).IsFalse();
        await Assert.That(expiredClipId).IsEqualTo(clipId);

        // Cache should be cleared after expiration
        await Task.Delay(50); // Small delay to ensure cleanup
        var cached = _service.GetDecryptedBlobs(clipId);
        await Assert.That(cached).IsNull();
    }

    [Test]
    public async Task ClearClip_WithPendingExpiration_CancelsCallback()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs = CreateTestBlobData();
        var callbackInvoked = false;

        _service.CacheDecryptedBlobs(
            clipId,
            blobs,
            TimeSpan.FromSeconds(1),
            _ => callbackInvoked = true);

        // Act - Clear before expiration
        await Task.Delay(50);
        _service.ClearClip(clipId);
        await Task.Delay(1500);

        // Assert - Callback should not have been invoked
        await Assert.That(callbackInvoked).IsFalse();
    }

    [Test]
    public async Task Dispose_CancelsAllTimers()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var blobs = CreateTestBlobData();
        var callback1Invoked = false;
        var callback2Invoked = false;

        _service.CacheDecryptedBlobs(clipId1, blobs, TimeSpan.FromSeconds(1), _ => callback1Invoked = true);
        _service.CacheDecryptedBlobs(clipId2, blobs, TimeSpan.FromSeconds(1), _ => callback2Invoked = true);

        // Act
        _service.Dispose();
        await Task.Delay(1500);

        // Assert - No callbacks should fire after disposal
        await Assert.That(callback1Invoked).IsFalse();
        await Assert.That(callback2Invoked).IsFalse();
    }

    [Test]
    public async Task CacheDecryptedBlobs_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs = CreateTestBlobData();
        _service.Dispose();

        // Act & Assert
        await Assert.That(() => _service.CacheDecryptedBlobs(clipId, blobs))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task GetAllCachedClipIds_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _service.Dispose();

        // Act & Assert
        await Assert.That(() => _service.GetAllCachedClipIds())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ClearAll_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _service.Dispose();

        // Act & Assert
        await Assert.That(() => _service.ClearAll())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task GetDecryptedBlobs_LogsDebugOnCacheHit()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs = CreateTestBlobData();
        _service.CacheDecryptedBlobs(clipId, blobs);

        // Act
        _service.GetDecryptedBlobs(clipId);

        // Assert - Should log debug message for cache hit
        _logger.Verify(
            p => p.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache hit")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task CacheDecryptedBlobs_WithCustomExpiration_LogsExpirationTime()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var blobs = CreateTestBlobData();

        // Act
        _service.CacheDecryptedBlobs(clipId, blobs, TimeSpan.FromMinutes(30));

        // Assert - Should log the expiration time
        _logger.Verify(
            p => p.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("30")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static DecryptedBlobData CreateTestBlobData()
    {
        return new DecryptedBlobData(
            [new BlobTxt { Id = Guid.NewGuid(), ClipId = Guid.NewGuid(), Data = "Test" }],
            [],
            [],
            []);
    }
}
