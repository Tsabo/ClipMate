using System.Text;
using ClipMate.Core.Models;
using ClipMate.Core.ValueObjects;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ClipService.EncryptClipsAsync method.
/// Tests encryption logic, metadata generation, BLOB encryption, and error handling.
/// </summary>
public partial class ClipServiceTests
{
    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_WithNullClipIds_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act & Assert
        await Assert.That(async () => await service.EncryptClipsAsync(TestDatabaseKey, null!, key))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_WithNullEncryptionKey_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateClipService();
        var clipIds = new List<Guid> { Guid.NewGuid() };

        // Act & Assert
        await Assert.That(async () => await service.EncryptClipsAsync(TestDatabaseKey, clipIds, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_WithNonExistentClip_SkipsAndReturnsZero()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert
        await Assert.That(result).IsEqualTo(0);
        MockEncryptionService.Verify(p => p.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionKey>(), It.IsAny<int>(), It.IsAny<EncryptionMetadata>()), Times.Never);
    }

    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_WithAlreadyEncryptedClip_SkipsAndReturnsZero()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clip = CreateTestClip(clipId, encrypted: true, encryptionSalt: "salt", encryptionIv: "iv");
        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert
        await Assert.That(result).IsEqualTo(0);
        MockEncryptionService.Verify(p => p.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionKey>(), It.IsAny<int>(), It.IsAny<EncryptionMetadata>()), Times.Never);
    }

    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_WithNoClipData_SkipsAndReturnsZero()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clip = CreateTestClip(clipId);
        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        MockClipDataRepository.Setup(p => p.GetByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClipData>()); // Empty list

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_WithTitle_EncryptsTitleAndGeneratesMetadata()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clip = CreateTestClip(clipId);
        clip.Title = "Secret Document";
        clip.CustomTitle = false;

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        var encryptedTitle = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        var metadata = new EncryptionMetadata("salt123", "iv456", "AES-256", 0);

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        MockClipDataRepository.Setup(p => p.GetByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClipData> { CreateTestClipData(clipId) });

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        MockEncryptionService
            .Setup(p => p.EncryptAsync(It.IsAny<byte[]>(), key, 600_000))
            .ReturnsAsync((Convert.FromBase64String(encryptedTitle), metadata));

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert - Title should be encrypted
        await Assert.That(result).IsEqualTo(1);
        await Assert.That(clip.Title).IsEqualTo(encryptedTitle);
        await Assert.That(clip.EncryptionSalt).IsEqualTo("salt123");
        await Assert.That(clip.EncryptionIv).IsEqualTo("iv456");
        await Assert.That(clip.EncryptionMethod).IsEqualTo("AES-256");
        await Assert.That(clip.Encrypted).IsTrue();
        await Assert.That(clip.IsDecrypted).IsFalse();

        MockEncryptionService.Verify(
            p => p.EncryptAsync(It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "Secret Document"), key, 600_000),
            Times.Once,
            "Title should be encrypted first");
    }

    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_WithCustomTitle_DoesNotEncryptTitle()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clip = CreateTestClip(clipId);
        clip.Title = "Custom Title";
        clip.CustomTitle = true; // Custom titles aren't encrypted

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        var textBlob = CreateTestTextBlob(clipId, "content");
        var metadata = new EncryptionMetadata("salt123", "iv456", "AES-256", 0);

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        MockClipDataRepository.Setup(p => p.GetByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClipData> { CreateTestClipData(clipId) });

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt> { textBlob });

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        MockEncryptionService
            .Setup(p => p.EncryptAsync(It.IsAny<byte[]>(), key, 600_000, null))
            .ReturnsAsync((new byte[] { 1, 2, 3 }, metadata));

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert - Title should NOT be encrypted (custom title)
        await Assert.That(result).IsEqualTo(1);
        await Assert.That(clip.Title).IsEqualTo("Custom Title"); // Unchanged

        // Encryption service should NOT be called with title text
        MockEncryptionService.Verify(
            p => p.EncryptAsync(It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "Custom Title"), It.IsAny<EncryptionKey>(), It.IsAny<int>(), It.IsAny<EncryptionMetadata>()),
            Times.Never,
            "Custom titles should not be encrypted");
    }

    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_WithTextBlob_EncryptsAndStoresAsBase64()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clip = CreateTestClip(clipId);
        clip.Title = null; // No title, metadata from first BLOB

        var textBlob = CreateTestTextBlob(clipId, "Plain text content");
        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        var encryptedBytes = new byte[] { 10, 20, 30, 40, 50 };
        var expectedBase64 = Convert.ToBase64String(encryptedBytes);
        var metadata = new EncryptionMetadata("salt789", "iv012", "AES-256", 0);

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        MockClipDataRepository.Setup(p => p.GetByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClipData> { CreateTestClipData(clipId) });

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt> { textBlob });

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        MockEncryptionService
            .Setup(p => p.EncryptAsync(It.IsAny<byte[]>(), key, 600_000, null))
            .ReturnsAsync((encryptedBytes, metadata));

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert
        await Assert.That(result).IsEqualTo(1);
        await Assert.That(textBlob.Data).IsEqualTo(expectedBase64); // Stored as Base64
        await Assert.That(clip.EncryptionSalt).IsEqualTo("salt789"); // Metadata from first BLOB
        await Assert.That(clip.EncryptionIv).IsEqualTo("iv012");

        MockBlobRepository.Verify(p => p.UpdateTextAsync(textBlob, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_ClearsTransientProperties()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clip = CreateTestClip(clipId);
        clip.TextContent = "Sensitive data";
        clip.RtfContent = "RTF data";
        clip.HtmlContent = "HTML data";
        clip.ImageData = [1, 2, 3];

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");
        var metadata = new EncryptionMetadata("salt", "iv", "AES-256", 0);

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        MockClipDataRepository.Setup(p => p.GetByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClipData> { CreateTestClipData(clipId) });

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        MockEncryptionService
            .Setup(p => p.EncryptAsync(It.IsAny<byte[]>(), key, It.IsAny<int>(), It.IsAny<EncryptionMetadata?>()))
            .ReturnsAsync((new byte[] { 1, 2, 3 }, metadata));

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert - Transient properties should be cleared for security
        await Assert.That(result).IsEqualTo(1);
        await Assert.That(clip.TextContent).IsNull();
        await Assert.That(clip.RtfContent).IsNull();
        await Assert.That(clip.HtmlContent).IsNull();
        await Assert.That(clip.ImageData).IsNull();
    }

    [Test]
    [Category("Encryption")]
    [Category("EncryptClipsAsync")]
    public async Task EncryptClipsAsync_WithException_LogsErrorAndContinues()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var clip1 = CreateTestClip(clipId1);
        var clip2 = CreateTestClip(clipId2);

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // First clip throws exception
        MockClipRepository.Setup(p => p.GetByIdAsync(clipId1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Second clip succeeds
        MockClipRepository.Setup(p => p.GetByIdAsync(clipId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip2);

        MockClipDataRepository.Setup(p => p.GetByClipIdAsync(clipId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClipData> { CreateTestClipData(clipId2) });

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clipId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clipId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clipId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clipId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId1, clipId2], key);

        // Assert - Should continue after error and process second clip
        await Assert.That(result).IsEqualTo(1);
    }
}
