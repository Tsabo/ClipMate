using ClipMate.Core.Models;
using ClipMate.Core.ValueObjects;
using Moq;

namespace ClipMate.Tests.Unit.Services;

[Category("ClipService")]
[Category("Unit")]
public partial class ClipServiceTests
{
    [Test]
    [Category("Decryption")]
    [Category("DecryptClipsAsync")]
    public async Task DecryptClipsAsync_WithNullClipIds_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        // Act & Assert
        await Assert.That(async () => await service.DecryptClipsAsync(TestDatabaseKey, null!, key))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipsAsync")]
    public async Task DecryptClipsAsync_WithNullEncryptionKey_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateClipService();
        var clipId = Guid.NewGuid();

        // Act & Assert
        await Assert.That(async () => await service.DecryptClipsAsync(TestDatabaseKey, [clipId], null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipsAsync")]
    public async Task DecryptClipsAsync_WithNonExistentClip_ReturnsZero()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        // Act
        var result = await service.DecryptClipsAsync(TestDatabaseKey, [clipId], key, true);

        // Assert
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipsAsync")]
    public async Task DecryptClipsAsync_WithNotEncryptedClip_SkipsAndReturnsZero()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clip = CreateTestClip(clipId);
        clip.Encrypted = false; // Not encrypted

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        // Act
        var result = await service.DecryptClipsAsync(TestDatabaseKey, [clipId], key, true);

        // Assert
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipsAsync")]
    public async Task DecryptClipsAsync_WithAlreadyDecryptedClip_SkipsAndReturnsZero()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clip = CreateTestClip(clipId);
        clip.Encrypted = true;
        clip.IsDecrypted = true; // Already decrypted

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        // Act
        var result = await service.DecryptClipsAsync(TestDatabaseKey, [clipId], key, true);

        // Assert
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    [Category("Decryption")]
    [Category("DecryptClipsAsync")]
    public async Task DecryptClipsAsync_WithMissingMetadata_SkipsAndReturnsZero()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var clip = CreateTestClip(clipId);
        clip.Encrypted = true;
        clip.IsDecrypted = false;
        clip.EncryptionSalt = null; // Missing metadata
        clip.EncryptionIv = null;
        clip.EncryptionMethod = null;

        var service = CreateClipService();
        using var key = EncryptionKey.FromPassphrase("test");

        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        // Act
        var result = await service.DecryptClipsAsync(TestDatabaseKey, [clipId], key, true);

        // Assert
        await Assert.That(result).IsEqualTo(0);
    }
}
