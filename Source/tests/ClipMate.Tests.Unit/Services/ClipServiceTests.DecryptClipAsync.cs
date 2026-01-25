using System.Text;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using Moq;

namespace ClipMate.Tests.Unit.Services;

[Category("ClipService")]
[Category("Unit")]
public partial class ClipServiceTests
{
    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_WithNotEncryptedClip_ReturnsFalse()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = false;

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_WithAlreadyDecryptedClip_ReturnsFalse()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = true; // Already decrypted

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_WithMissingSalt_ReturnsFalse()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = null; // Missing
        clip.EncryptionIv = "iv123";
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_WithMissingIv_ReturnsFalse()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = null; // Missing
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_Permanent_ClearsEncryptionMetadata()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.Encrypted).IsFalse();
        await Assert.That(clip.IsDecrypted).IsFalse();
        await Assert.That(clip.EncryptionSalt).IsNull();
        await Assert.That(clip.EncryptionIv).IsNull();
        await Assert.That(clip.EncryptionMethod).IsNull();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_Temporary_SetsIsDecryptedFlag()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test", 30);

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.IsDecrypted).IsTrue();
        await Assert.That(clip.Encrypted).IsTrue(); // Should remain true for temporary
        await Assert.That(clip.EncryptionSalt).IsEqualTo("salt123"); // Should remain for temporary
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_Temporary_CachesDecryptedBlobs()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test", 30);

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        // Act
        await service.DecryptClipAsync(TestDatabaseKey, clip, key);

        // Assert - Verify cache was called with correct parameters
        MockBlobCacheService.Verify(
            x => x.CacheDecryptedBlobs(
                clip.Id,
                It.IsAny<DecryptedBlobData>(),
                TimeSpan.FromMinutes(30),
                It.IsAny<Action<Guid>>()),
            Times.Once);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_DecryptsTextBlob_WithBase64Encoding()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var textBlob = CreateTestTextBlob(clip.Id);
        textBlob.Data = Convert.ToBase64String(new byte[] { 1, 2, 3 }); // Encrypted as Base64

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        var metadata = new EncryptionMetadata("salt123", "iv456", "AES-256", 0);
        var decryptedBytes = "Decrypted Text Content"u8.ToArray();

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt> { textBlob });

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        MockEncryptionService
            .Setup(p => p.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionMetadata>(), key, 600_000))
            .ReturnsAsync(decryptedBytes);

        MockBlobRepository.Setup(p => p.UpdateTextAsync(It.IsAny<BlobTxt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(textBlob.Data).IsEqualTo("Decrypted Text Content");
        MockBlobRepository.Verify(p => p.UpdateTextAsync(textBlob, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_DecryptsJpgBlob_WithBinaryData()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var jpgBlob = CreateTestJpgBlob(clip.Id);
        jpgBlob.Data = [1, 2, 3]; // Encrypted bytes

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        var decryptedBytes = new byte[] { 10, 20, 30, 40 };

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg> { jpgBlob });

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        MockEncryptionService
            .Setup(p => p.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionMetadata>(), key, 600_000))
            .ReturnsAsync(decryptedBytes);

        MockBlobRepository.Setup(p => p.UpdateJpgAsync(It.IsAny<BlobJpg>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(jpgBlob.Data).IsEqualTo(decryptedBytes);
        MockBlobRepository.Verify(p => p.UpdateJpgAsync(jpgBlob, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_DecryptsPngBlob_WithBinaryData()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var pngBlob = CreateTestPngBlob(clip.Id);
        pngBlob.Data = [1, 2, 3]; // Encrypted bytes

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        var decryptedBytes = new byte[] { 10, 20, 30, 40 };

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng> { pngBlob });

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        MockEncryptionService
            .Setup(p => p.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionMetadata>(), key, 600_000))
            .ReturnsAsync(decryptedBytes);

        MockBlobRepository.Setup(p => p.UpdatePngAsync(It.IsAny<BlobPng>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(pngBlob.Data).IsEqualTo(decryptedBytes);
        MockBlobRepository.Verify(p => p.UpdatePngAsync(pngBlob, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_DecryptsBinaryBlob_WithBinaryData()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var binaryBlob = CreateTestBinaryBlob(clip.Id);
        binaryBlob.Data = [1, 2, 3]; // Encrypted bytes

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        var decryptedBytes = new byte[] { 10, 20, 30, 40 };

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob> { binaryBlob });

        MockEncryptionService
            .Setup(p => p.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionMetadata>(), key, 600_000))
            .ReturnsAsync(decryptedBytes);

        MockBlobRepository.Setup(p => p.UpdateBlobAsync(It.IsAny<BlobBlob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(binaryBlob.Data).IsEqualTo(decryptedBytes);
        MockBlobRepository.Verify(p => p.UpdateBlobAsync(binaryBlob, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_DecryptsTitle_WhenLooksEncrypted()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.CustomTitle = false;
        clip.Title = Convert.ToBase64String(new byte[50]); // Long Base64 string (> 44 chars)
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        var decryptedTitleBytes = "Decrypted Title"u8.ToArray();

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        MockEncryptionService
            .Setup(p => p.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionMetadata>(), key, 600_000))
            .ReturnsAsync(decryptedTitleBytes);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.Title).IsEqualTo("Decrypted Title");
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_SkipsTitleDecryption_WhenTooShort()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.CustomTitle = false;
        clip.Title = "ShortTitle"; // Less than 44 chars - doesn't look encrypted
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.Title).IsEqualTo("ShortTitle"); // Unchanged
        MockEncryptionService.Verify(
            p => p.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionMetadata>(), key, 600_000),
            Times.Never);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_SkipsTitleDecryption_WhenCustomTitle()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.CustomTitle = true; // Custom title shouldn't be decrypted
        clip.Title = Convert.ToBase64String(new byte[50]); // Long Base64
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        var originalTitle = clip.Title;

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.Title).IsEqualTo(originalTitle); // Unchanged
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_HandlesTextBlobDecryptionException_ContinuesWithOthers()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var textBlob1 = CreateTestTextBlob(clip.Id);
        textBlob1.Data = "InvalidBase64!@#$"; // Will cause FormatException

        var textBlob2 = CreateTestTextBlob(clip.Id);
        textBlob2.Data = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt> { textBlob1, textBlob2 });

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        var decryptedBytes = "Decrypted"u8.ToArray();
        MockEncryptionService
            .Setup(p => p.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionMetadata>(), key, 600_000))
            .ReturnsAsync(decryptedBytes);

        MockBlobRepository.Setup(p => p.UpdateTextAsync(It.IsAny<BlobTxt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert - Should succeed and decrypt the valid blob
        await Assert.That(result).IsTrue();
        await Assert.That(textBlob1.Data).IsEqualTo("InvalidBase64!@#$"); // Unchanged due to error
        await Assert.That(textBlob2.Data).IsEqualTo("Decrypted");
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_Temporary_DoesNotPersistBlobUpdates()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var textBlob = CreateTestTextBlob(clip.Id);
        textBlob.Data = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test", 30);

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt> { textBlob });

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg>());

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng>());

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob>());

        var decryptedBytes = "Decrypted"u8.ToArray();
        MockEncryptionService
            .Setup(p => p.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionMetadata>(), key, 600_000))
            .ReturnsAsync(decryptedBytes);

        // Act
        await service.DecryptClipAsync(TestDatabaseKey, clip, key);

        // Assert - Blob update methods should NOT be called for temporary decryption
        MockBlobRepository.Verify(p => p.UpdateTextAsync(It.IsAny<BlobTxt>(), It.IsAny<CancellationToken>()), Times.Never);
        MockBlobRepository.Verify(p => p.UpdateJpgAsync(It.IsAny<BlobJpg>(), It.IsAny<CancellationToken>()), Times.Never);
        MockBlobRepository.Verify(p => p.UpdatePngAsync(It.IsAny<BlobPng>(), It.IsAny<CancellationToken>()), Times.Never);
        MockBlobRepository.Verify(p => p.UpdateBlobAsync(It.IsAny<BlobBlob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_Permanent_PersistsAllBlobUpdates()
    {
        // Arrange
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var textBlob = CreateTestTextBlob(clip.Id);
        textBlob.Data = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        var jpgBlob = CreateTestJpgBlob(clip.Id);
        jpgBlob.Data = [1, 2, 3];

        var pngBlob = CreateTestPngBlob(clip.Id);
        pngBlob.Data = [1, 2, 3];

        var binaryBlob = CreateTestBinaryBlob(clip.Id);
        binaryBlob.Data = [1, 2, 3];

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockBlobRepository.Setup(p => p.GetTextByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt> { textBlob });

        MockBlobRepository.Setup(p => p.GetJpgByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobJpg> { jpgBlob });

        MockBlobRepository.Setup(p => p.GetPngByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobPng> { pngBlob });

        MockBlobRepository.Setup(p => p.GetBlobByClipIdAsync(clip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobBlob> { binaryBlob });

        var decryptedBytes = new byte[] { 10, 20, 30 };
        MockEncryptionService
            .Setup(p => p.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionMetadata>(), key, 600_000))
            .ReturnsAsync(decryptedBytes);

        MockBlobRepository.Setup(p => p.UpdateTextAsync(It.IsAny<BlobTxt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        MockBlobRepository.Setup(p => p.UpdateJpgAsync(It.IsAny<BlobJpg>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        MockBlobRepository.Setup(p => p.UpdatePngAsync(It.IsAny<BlobPng>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        MockBlobRepository.Setup(p => p.UpdateBlobAsync(It.IsAny<BlobBlob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert - All BLOB update methods should be called
        MockBlobRepository.Verify(p => p.UpdateTextAsync(textBlob, It.IsAny<CancellationToken>()), Times.Once);
        MockBlobRepository.Verify(p => p.UpdateJpgAsync(jpgBlob, It.IsAny<CancellationToken>()), Times.Once);
        MockBlobRepository.Verify(p => p.UpdatePngAsync(pngBlob, It.IsAny<CancellationToken>()), Times.Once);
        MockBlobRepository.Verify(p => p.UpdateBlobAsync(binaryBlob, It.IsAny<CancellationToken>()), Times.Once);
    }
}
