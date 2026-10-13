using System.Text;
using ClipMate.Core.Models;
using ClipMate.Core.ValueObjects;
using FakeItEasy;

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

        A.CallTo(() => MockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns((Clip?)null);

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert
        await Assert.That(result).IsEqualTo(0);
        A.CallTo(() => MockEncryptionService.EncryptAsync(A<byte[]>.Ignored, A<EncryptionKey>.Ignored, A<int>.Ignored, A<EncryptionMetadata>.Ignored)).MustNotHaveHappened();
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

        A.CallTo(() => MockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(clip);

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert
        await Assert.That(result).IsEqualTo(0);
        A.CallTo(() => MockEncryptionService.EncryptAsync(A<byte[]>.Ignored, A<EncryptionKey>.Ignored, A<int>.Ignored, A<EncryptionMetadata>.Ignored)).MustNotHaveHappened();
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

        A.CallTo(() => MockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(clip);

        A.CallTo(() => MockClipDataRepository.GetByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<ClipData>()); // Empty list

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

        A.CallTo(() => MockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(clip);

        A.CallTo(() => MockClipDataRepository.GetByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<ClipData> { CreateTestClipData(clipId) });

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        A.CallTo(() => MockEncryptionService.EncryptAsync(A<byte[]>.Ignored, key, 600_000))
            .Returns((Convert.FromBase64String(encryptedTitle), metadata));

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

        A.CallTo(() => MockEncryptionService.EncryptAsync(A<byte[]>.That.Matches(b => Encoding.UTF8.GetString(b) == "Secret Document"), key, 600_000)).MustHaveHappenedOnceExactly(); // Title should be encrypted first
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

        A.CallTo(() => MockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(clip);

        A.CallTo(() => MockClipDataRepository.GetByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<ClipData> { CreateTestClipData(clipId) });

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt> { textBlob });

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        A.CallTo(() => MockEncryptionService.EncryptAsync(A<byte[]>.Ignored, key, 600_000, null))
            .Returns((new byte[] { 1, 2, 3 }, metadata));

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert - Title should NOT be encrypted (custom title)
        await Assert.That(result).IsEqualTo(1);
        await Assert.That(clip.Title).IsEqualTo("Custom Title"); // Unchanged

        // Encryption service should NOT be called with title text
        A.CallTo(() => MockEncryptionService.EncryptAsync(A<byte[]>.That.Matches(b => Encoding.UTF8.GetString(b) == "Custom Title"), A<EncryptionKey>.Ignored, A<int>.Ignored, A<EncryptionMetadata>.Ignored)).MustNotHaveHappened(); // Custom titles should not be encrypted
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

        A.CallTo(() => MockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(clip);

        A.CallTo(() => MockClipDataRepository.GetByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<ClipData> { CreateTestClipData(clipId) });

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt> { textBlob });

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        A.CallTo(() => MockEncryptionService.EncryptAsync(A<byte[]>.Ignored, key, 600_000, null))
            .Returns((encryptedBytes, metadata));

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId], key);

        // Assert
        await Assert.That(result).IsEqualTo(1);
        await Assert.That(textBlob.Data).IsEqualTo(expectedBase64); // Stored as Base64
        await Assert.That(clip.EncryptionSalt).IsEqualTo("salt789"); // Metadata from first BLOB
        await Assert.That(clip.EncryptionIv).IsEqualTo("iv012");

        A.CallTo(() => MockBlobRepository.UpdateTextAsync(textBlob, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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

        A.CallTo(() => MockClipRepository.GetByIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(clip);

        A.CallTo(() => MockClipDataRepository.GetByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<ClipData> { CreateTestClipData(clipId) });

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        A.CallTo(() => MockEncryptionService.EncryptAsync(A<byte[]>.Ignored, key, A<int>.Ignored, A<EncryptionMetadata?>.Ignored))
            .Returns((new byte[] { 1, 2, 3 }, metadata));

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
        A.CallTo(() => MockClipRepository.GetByIdAsync(clipId1, A<CancellationToken>.Ignored))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Second clip succeeds
        A.CallTo(() => MockClipRepository.GetByIdAsync(clipId2, A<CancellationToken>.Ignored))
            .Returns(clip2);

        A.CallTo(() => MockClipDataRepository.GetByClipIdAsync(clipId2, A<CancellationToken>.Ignored))
            .Returns(new List<ClipData> { CreateTestClipData(clipId2) });

        A.CallTo(() => MockBlobRepository.GetTextByClipIdAsync(clipId2, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        A.CallTo(() => MockBlobRepository.GetJpgByClipIdAsync(clipId2, A<CancellationToken>.Ignored))
            .Returns(new List<BlobJpg>());

        A.CallTo(() => MockBlobRepository.GetPngByClipIdAsync(clipId2, A<CancellationToken>.Ignored))
            .Returns(new List<BlobPng>());

        A.CallTo(() => MockBlobRepository.GetBlobByClipIdAsync(clipId2, A<CancellationToken>.Ignored))
            .Returns(new List<BlobBlob>());

        // Act
        var result = await service.EncryptClipsAsync(TestDatabaseKey, [clipId1, clipId2], key);

        // Assert - Should continue after error and process second clip
        await Assert.That(result).IsEqualTo(1);
    }
}
