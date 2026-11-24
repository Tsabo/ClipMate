using System.Text;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using FakeItEasy;

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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        // Act
        await service.DecryptClipAsync(TestDatabaseKey, clip, key);

        // Assert - Verify cache was called with correct parameters
        A.CallTo(() => MockBlobCacheService.CacheDecryptedBlobs(
                clip.Id,
                A<DecryptedBlobData>.Ignored,
                TimeSpan.FromMinutes(30),
                A<Action<Guid>>.Ignored)).MustHaveHappenedOnceExactly();
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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt> { textBlob });

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, key, 600_000))
            .Returns(decryptedBytes);

        A.CallTo(() => MockBlobRepository.UpdateTextAsync(A<BlobTxt>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(textBlob.Data).IsEqualTo("Decrypted Text Content");
        A.CallTo(() => MockBlobRepository.UpdateTextAsync(textBlob, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg> { jpgBlob });

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, key, 600_000))
            .Returns(decryptedBytes);

        A.CallTo(() => MockBlobRepository.UpdateJpgAsync(A<BlobJpg>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(jpgBlob.Data).IsEqualTo(decryptedBytes);
        A.CallTo(() => MockBlobRepository.UpdateJpgAsync(jpgBlob, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng> { pngBlob });

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, key, 600_000))
            .Returns(decryptedBytes);

        A.CallTo(() => MockBlobRepository.UpdatePngAsync(A<BlobPng>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(pngBlob.Data).IsEqualTo(decryptedBytes);
        A.CallTo(() => MockBlobRepository.UpdatePngAsync(pngBlob, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob> { binaryBlob });

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, key, 600_000))
            .Returns(decryptedBytes);

        A.CallTo(() => MockBlobRepository.UpdateBlobAsync(A<BlobBlob>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(binaryBlob.Data).IsEqualTo(decryptedBytes);
        A.CallTo(() => MockBlobRepository.UpdateBlobAsync(binaryBlob, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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
        clip.Title = Convert.ToBase64String(new byte[48]); // A legacy CBC title: checksum block plus whole data blocks
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        var decryptedTitleBytes = "Decrypted Title"u8.ToArray();

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, key, 600_000))
            .Returns(decryptedTitleBytes);

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
        clip.Title = "ShortTitle"; // Too short to be ciphertext - doesn't look encrypted
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = "iv456";
        clip.EncryptionMethod = "AES-256";

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.Title).IsEqualTo("ShortTitle"); // Unchanged
        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, key, 600_000)).MustNotHaveHappened();
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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt> { textBlob1, textBlob2 });

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        var decryptedBytes = "Decrypted"u8.ToArray();
        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, key, 600_000))
            .Returns(decryptedBytes);

        A.CallTo(() => MockBlobRepository.UpdateTextAsync(A<BlobTxt>.Ignored, A<CancellationToken>.Ignored))
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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt> { textBlob });

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        var decryptedBytes = "Decrypted"u8.ToArray();
        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, key, 600_000))
            .Returns(decryptedBytes);

        // Act
        await service.DecryptClipAsync(TestDatabaseKey, clip, key);

        // Assert - Blob update methods should NOT be called for temporary decryption
        A.CallTo(() => MockBlobRepository.UpdateTextAsync(A<BlobTxt>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => MockBlobRepository.UpdateJpgAsync(A<BlobJpg>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => MockBlobRepository.UpdatePngAsync(A<BlobPng>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => MockBlobRepository.UpdateBlobAsync(A<BlobBlob>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
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

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt> { textBlob });

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg> { jpgBlob });

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng> { pngBlob });

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob> { binaryBlob });

        var decryptedBytes = new byte[] { 10, 20, 30 };
        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, key, 600_000))
            .Returns(decryptedBytes);

        A.CallTo(() => MockBlobRepository.UpdateTextAsync(A<BlobTxt>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        A.CallTo(() => MockBlobRepository.UpdateJpgAsync(A<BlobJpg>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        A.CallTo(() => MockBlobRepository.UpdatePngAsync(A<BlobPng>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        A.CallTo(() => MockBlobRepository.UpdateBlobAsync(A<BlobBlob>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert - All BLOB update methods should be called
        A.CallTo(() => MockBlobRepository.UpdateTextAsync(textBlob, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        A.CallTo(() => MockBlobRepository.UpdateJpgAsync(jpgBlob, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        A.CallTo(() => MockBlobRepository.UpdatePngAsync(pngBlob, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        A.CallTo(() => MockBlobRepository.UpdateBlobAsync(binaryBlob, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_AesGcmClipWithoutIv_Decrypts()
    {
        // Arrange: AES-GCM keeps its nonce in each field, so the clip has no IV
        var clip = CreateEncryptedClip(EncryptionMetadata.AesGcmAlgorithm, iv: null);
        var textBlob = CreateTestTextBlob(clip.Id);
        textBlob.Data = Convert.ToBase64String(new byte[40]);
        SetupBlobs(clip.Id, [textBlob]);

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, A<EncryptionKey>.Ignored, 600_000))
            .Returns("Plain"u8.ToArray());

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(textBlob.Data).IsEqualTo("Plain");
        await Assert.That(clip.Encrypted).IsFalse();
        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored,
                A<EncryptionMetadata>.That.Matches(p => p.IsAuthenticated), A<EncryptionKey>.Ignored, 600_000))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_AesGcmAuthenticationFailure_ThrowsAndLeavesClipEncrypted()
    {
        // Arrange: the first field decrypts, the second fails authentication (wrong key or altered data)
        var clip = CreateEncryptedClip(EncryptionMetadata.AesGcmAlgorithm, iv: null);
        var textBlob = CreateTestTextBlob(clip.Id);
        var encryptedText = Convert.ToBase64String(new byte[40]);
        textBlob.Data = encryptedText;
        var jpgBlob = CreateTestJpgBlob(clip.Id);
        jpgBlob.Data = [9, 9, 9];
        SetupBlobs(clip.Id, [textBlob], [jpgBlob]);

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, A<EncryptionKey>.Ignored, 600_000))
            .ReturnsNextFromSequence("Plain"u8.ToArray());

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.That.Matches(p => p.Length == 3), A<EncryptionMetadata>.Ignored, A<EncryptionKey>.Ignored, 600_000))
            .ThrowsAsync(new System.Security.Cryptography.CryptographicException("Authentication failed."));

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("wrong");

        // Act & Assert
        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(async () =>
            await service.DecryptClipAsync(TestDatabaseKey, clip, key, true));

        // Nothing written, and the stored entities are untouched
        await Assert.That(clip.Encrypted).IsTrue();
        await Assert.That(clip.EncryptionSalt).IsEqualTo("salt123");
        await Assert.That(textBlob.Data).IsEqualTo(encryptedText);
        AssertNothingPersisted();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_LegacyWrongKey_ThrowsAndLeavesClipEncrypted()
    {
        // Arrange: every field fails, so the key is wrong
        var clip = CreateEncryptedClip(EncryptionMetadata.LegacyCbcAlgorithm, iv: "iv456");
        var textBlob = CreateTestTextBlob(clip.Id);
        var encryptedText = Convert.ToBase64String(new byte[32]);
        textBlob.Data = encryptedText;
        var pngBlob = CreateTestPngBlob(clip.Id);
        pngBlob.Data = [1, 2, 3];
        SetupBlobs(clip.Id, [textBlob], png: [pngBlob]);

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, A<EncryptionKey>.Ignored, 600_000))
            .ThrowsAsync(new System.Security.Cryptography.CryptographicException("Checksum validation failed."));

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("wrong");

        // Act & Assert
        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(async () =>
            await service.DecryptClipAsync(TestDatabaseKey, clip, key, true));

        await Assert.That(clip.Encrypted).IsTrue();
        await Assert.That(clip.EncryptionIv).IsEqualTo("iv456");
        await Assert.That(textBlob.Data).IsEqualTo(encryptedText);
        AssertNothingPersisted();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_LegacyWrongKey_ForViewing_ThrowsWithoutCaching()
    {
        // Arrange
        var clip = CreateEncryptedClip(EncryptionMetadata.LegacyCbcAlgorithm, iv: "iv456");
        clip.Title = Convert.ToBase64String(new byte[32]);
        var textBlob = CreateTestTextBlob(clip.Id);
        textBlob.Data = Convert.ToBase64String(new byte[32]);
        SetupBlobs(clip.Id, [textBlob]);

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, A<EncryptionKey>.Ignored, 600_000))
            .ThrowsAsync(new System.Security.Cryptography.CryptographicException("Checksum validation failed."));

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("wrong", 30);

        // Act & Assert: an error, not a placeholder title and cached ciphertext
        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(async () =>
            await service.DecryptClipAsync(TestDatabaseKey, clip, key));

        await Assert.That(clip.IsDecrypted).IsFalse();
        await Assert.That(clip.Title).IsEqualTo(Convert.ToBase64String(new byte[32]));
        A.CallTo(() => MockBlobCacheService.CacheDecryptedBlobs(A<Guid>.Ignored, A<DecryptedBlobData>.Ignored, A<TimeSpan>.Ignored, A<Action<Guid>>.Ignored))
            .MustNotHaveHappened();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_LegacyFieldAlreadyPlain_IsLeftAsIs()
    {
        // Arrange: one field is already plain text (old bug); the rest decrypt, so the key is right
        var clip = CreateEncryptedClip(EncryptionMetadata.LegacyCbcAlgorithm, iv: "iv456");
        var textBlob = CreateTestTextBlob(clip.Id);
        textBlob.Data = Convert.ToBase64String(new byte[32]);
        var jpgBlob = CreateTestJpgBlob(clip.Id);
        var plainJpg = new byte[] { 0xFF, 0xD8, 0xFF };
        jpgBlob.Data = plainJpg;
        SetupBlobs(clip.Id, [textBlob], [jpgBlob]);

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, A<EncryptionKey>.Ignored, 600_000))
            .Returns("Plain"u8.ToArray());

        A.CallTo(() => MockEncryptionService.DecryptAsync(plainJpg, A<EncryptionMetadata>.Ignored, A<EncryptionKey>.Ignored, 600_000))
            .ThrowsAsync(new System.Security.Cryptography.CryptographicException("Checksum validation failed."));

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(textBlob.Data).IsEqualTo("Plain");
        await Assert.That(jpgBlob.Data).IsEqualTo(plainJpg);
        await Assert.That(clip.Encrypted).IsFalse();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_DecryptsShortLegacyTitle()
    {
        // Arrange: a title under 16 bytes encrypts to 32 bytes, 44 Base64 characters
        var clip = CreateEncryptedClip(EncryptionMetadata.LegacyCbcAlgorithm, iv: "iv456");
        clip.Title = Convert.ToBase64String(new byte[32]);
        SetupBlobs(clip.Id);

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, A<EncryptionKey>.Ignored, 600_000))
            .Returns("Hi"u8.ToArray());

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.Title).IsEqualTo("Hi");
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipAsync")]
    public async Task DecryptClipAsync_DecryptsShortAesGcmTitle()
    {
        // Arrange: a one-byte title is the version byte, nonce, one byte and tag (30 bytes)
        var clip = CreateEncryptedClip(EncryptionMetadata.AesGcmAlgorithm, iv: null);
        var encryptedTitle = new byte[EncryptionMetadata.AesGcmOverhead + 1];
        encryptedTitle[0] = EncryptionMetadata.AesGcmFormatVersion;
        clip.Title = Convert.ToBase64String(encryptedTitle);
        SetupBlobs(clip.Id);

        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>.Ignored, A<EncryptionMetadata>.Ignored, A<EncryptionKey>.Ignored, 600_000))
            .Returns("X"u8.ToArray());

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act
        var result = await service.DecryptClipAsync(TestDatabaseKey, clip, key, true);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.Title).IsEqualTo("X");
    }

    private static Clip CreateEncryptedClip(string algorithm, string? iv)
    {
        var clip = CreateTestClip();
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.CustomTitle = false;
        clip.Title = null;
        clip.EncryptionSalt = "salt123";
        clip.EncryptionIv = iv;
        clip.EncryptionMethod = algorithm;
        return clip;
    }

    private void SetupBlobs(Guid clipId, List<BlobTxt>? text = null, List<BlobJpg>? jpg = null, List<BlobPng>? png = null, List<BlobBlob>? binary = null)
    {
        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clipId, A<CancellationToken>.Ignored)).Returns(text ?? []);
        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clipId, A<CancellationToken>.Ignored)).Returns(jpg ?? []);
        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clipId, A<CancellationToken>.Ignored)).Returns(png ?? []);
        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clipId, A<CancellationToken>.Ignored)).Returns(binary ?? []);
    }

    private void AssertNothingPersisted()
    {
        A.CallTo(() => MockBlobRepository.UpdateTextAsync(A<BlobTxt>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => MockBlobRepository.UpdateJpgAsync(A<BlobJpg>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => MockBlobRepository.UpdatePngAsync(A<BlobPng>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => MockBlobRepository.UpdateBlobAsync(A<BlobBlob>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => MockClipRepository.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }
}
