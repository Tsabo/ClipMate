using System.Security.Cryptography;
using System.Text;
using ClipMate.Core.ValueObjects;
using ClipMate.Platform.Services;

namespace ClipMate.Tests.Unit.Services;

public class EncryptionServiceTests : TestFixtureBase
{
    private const int _testIterations = 100000; // Minimum for tests (faster than 600K)
    private AesEncryptionService _service = null!;

    [Before(Test)]
    public void Setup()
    {
        _service = new AesEncryptionService();
    }

    [Test]
    public async Task EncryptAsync_WithValidData_ReturnsEncryptedDataAndMetadata()
    {
        // Arrange
        var plaintext = "Hello, World!"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("test-password-123");

        // Act
        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Assert
        await Assert.That(encryptedData).IsNotNull();
        await Assert.That(encryptedData.Length).IsGreaterThan(plaintext.Length);
        await Assert.That(metadata.Algorithm).IsEqualTo(EncryptionMetadata.AesGcmAlgorithm);
        await Assert.That(metadata.Salt).IsNotEmpty();
        await Assert.That(metadata.IV).IsEmpty(); // AES-GCM keeps its nonce in the data
    }

    [Test]
    public async Task DecryptAsync_WithCorrectKey_ReturnsOriginalData()
    {
        // Arrange
        var plaintext = "Sensitive data to encrypt"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("secure-passphrase");

        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Act
        var decryptedData = await _service.DecryptAsync(encryptedData, metadata, key, _testIterations);

        // Assert
        await Assert.That(decryptedData).IsEquivalentTo(plaintext);
    }

    [Test]
    public async Task EncryptAsync_SameDataDifferentCalls_ProducesDifferentCiphertext()
    {
        // Arrange
        var plaintext = "Same data"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var (encrypted1, _) = await _service.EncryptAsync(plaintext, key, _testIterations);
        var (encrypted2, _) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Assert - Different salt/IV should produce different ciphertext
        await Assert.That(encrypted1).IsNotEquivalentTo(encrypted2);
    }

    [Test]
    public async Task DecryptAsync_WithWrongKey_ThrowsCryptographicException()
    {
        // Arrange
        var plaintext = "Secret message"u8.ToArray();
        using var correctKey = EncryptionKey.FromPassphrase("correct-password");
        using var wrongKey = EncryptionKey.FromPassphrase("wrong-password");

        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, correctKey, _testIterations);

        // Act & Assert
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _service.DecryptAsync(encryptedData, metadata, wrongKey, _testIterations));
    }

    [Test]
    public async Task DecryptAsync_WithCorruptedData_ThrowsCryptographicException()
    {
        // Arrange
        var plaintext = "Data to corrupt"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("password");

        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Corrupt the data
        encryptedData[encryptedData.Length / 2] ^= 0xFF;

        // Act & Assert
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _service.DecryptAsync(encryptedData, metadata, key, _testIterations));
    }

    [Test]
    public async Task EncryptAsync_WithEmptyData_EncryptsSuccessfully()
    {
        // Arrange
        var plaintext = Array.Empty<byte>();
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Assert
        await Assert.That(encryptedData).IsNotNull();
        await Assert.That(encryptedData.Length).IsEqualTo(EncryptionMetadata.AesGcmOverhead); // Version, nonce and tag
    }

    [Test]
    public async Task DecryptAsync_WithEmptyEncryptedData_ReturnsEmptyData()
    {
        // Arrange
        var plaintext = Array.Empty<byte>();
        using var key = EncryptionKey.FromPassphrase("password");

        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Act
        var decryptedData = await _service.DecryptAsync(encryptedData, metadata, key, _testIterations);

        // Assert
        await Assert.That(decryptedData).IsEmpty();
    }

    [Test]
    public async Task EncryptAsync_WithLargeData_EncryptsSuccessfully()
    {
        // Arrange
        var plaintext = new byte[1024 * 1024]; // 1 MB
        Random.Shared.NextBytes(plaintext);
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);
        var decryptedData = await _service.DecryptAsync(encryptedData, metadata, key, _testIterations);

        // Assert: compared in order, as bytes; IsEquivalentTo matches a megabyte element by element, ignoring order, and
        // took over a minute on CI runners
        await Assert.That(decryptedData.AsSpan().SequenceEqual(plaintext)).IsTrue();
    }

    [Test]
    public async Task EncryptAsync_WithUnicodeText_PreservesEncoding()
    {
        // Arrange
        const string text = "Hello 世界 🔒🔓";
        var plaintext = Encoding.UTF8.GetBytes(text);
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);
        var decryptedData = await _service.DecryptAsync(encryptedData, metadata, key, _testIterations);
        var decryptedText = Encoding.UTF8.GetString(decryptedData);

        // Assert
        await Assert.That(decryptedText).IsEqualTo(text);
    }

    [Test]
    public async Task DeriveKeyAsync_WithSameInputs_ProducesSameKey()
    {
        // Arrange
        var salt = new byte[16];
        Random.Shared.NextBytes(salt);
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var derivedKey1 = await _service.DeriveKeyAsync(key, salt, _testIterations);
        var derivedKey2 = await _service.DeriveKeyAsync(key, salt, _testIterations);

        // Assert
        await Assert.That(derivedKey1).IsEquivalentTo(derivedKey2);
    }

    [Test]
    public async Task DeriveKeyAsync_WithDifferentSalts_ProducesDifferentKeys()
    {
        // Arrange
        var salt1 = new byte[16];
        var salt2 = new byte[16];
        Random.Shared.NextBytes(salt1);
        Random.Shared.NextBytes(salt2);
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var derivedKey1 = await _service.DeriveKeyAsync(key, salt1, _testIterations);
        var derivedKey2 = await _service.DeriveKeyAsync(key, salt2, _testIterations);

        // Assert
        await Assert.That(derivedKey1).IsNotEquivalentTo(derivedKey2);
    }

    [Test]
    public async Task DeriveKeyAsync_Returns32ByteKey()
    {
        // Arrange
        var salt = new byte[16];
        Random.Shared.NextBytes(salt);
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var derivedKey = await _service.DeriveKeyAsync(key, salt, _testIterations);

        // Assert
        await Assert.That(derivedKey.Length).IsEqualTo(32); // 256 bits / 8 = 32 bytes
    }

    [Test]
    public async Task EncryptAsync_WithTooFewIterations_ThrowsArgumentException()
    {
        // Arrange
        var plaintext = "data"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("password");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.EncryptAsync(plaintext, key, 50000)); // Below 100K minimum
    }

    [Test]
    public async Task DecryptAsync_WithTooFewIterations_ThrowsArgumentException()
    {
        // Arrange
        var plaintext = "data"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("password");
        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.DecryptAsync(encryptedData, metadata, key, 50000)); // Below 100K minimum
    }

    [Test]
    public async Task EncryptAsync_WithNullData_ThrowsArgumentNullException()
    {
        // Arrange
        using var key = EncryptionKey.FromPassphrase("password");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.EncryptAsync(null!, key, _testIterations));
    }

    [Test]
    public async Task DecryptAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var plaintext = "data"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("password");
        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.DecryptAsync(encryptedData, metadata, null!, _testIterations));
    }

    [Test]
    public async Task EncryptAsync_ChecksumValidation_DetectsIncorrectKey()
    {
        // Arrange
        var plaintext = "Important data"u8.ToArray();
        using var correctKey = EncryptionKey.FromPassphrase("correct-key-12345");
        using var wrongKey = EncryptionKey.FromPassphrase("wrong-key-67890");

        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, correctKey, _testIterations);

        // Act & Assert - Should throw due to authentication failure
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _service.DecryptAsync(encryptedData, metadata, wrongKey, _testIterations));
    }

    [Test]
    public async Task EncryptAsync_WritesVersionNonceCiphertextAndTag()
    {
        // Arrange
        var plaintext = "Hello, World!"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var (encryptedData, _) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Assert
        await Assert.That(encryptedData.Length).IsEqualTo(plaintext.Length + EncryptionMetadata.AesGcmOverhead);
        await Assert.That(encryptedData[0]).IsEqualTo(EncryptionMetadata.AesGcmFormatVersion);
    }

    [Test]
    public async Task EncryptAsync_ReusingMetadata_SharesSaltButNeverNonce()
    {
        // Arrange: the fields of one clip share its salt, as ClipContentEncryption does
        var plaintext = "The same first line of a clip, used as its title"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var (first, firstMetadata) = await _service.EncryptAsync(plaintext, key, _testIterations);
        var (second, secondMetadata) = await _service.EncryptAsync(plaintext, key, _testIterations, firstMetadata);

        // Assert: same salt, different nonce, and the same plaintext doesn't give the same ciphertext (issue #63)
        await Assert.That(secondMetadata.Salt).IsEqualTo(firstMetadata.Salt);
        await Assert.That(first.AsSpan(1, 12).SequenceEqual(second.AsSpan(1, 12))).IsFalse();
        await Assert.That(first.AsSpan(13, 16).SequenceEqual(second.AsSpan(13, 16))).IsFalse();

        await Assert.That(await _service.DecryptAsync(first, firstMetadata, key, _testIterations)).IsEquivalentTo(plaintext);
        await Assert.That(await _service.DecryptAsync(second, secondMetadata, key, _testIterations)).IsEquivalentTo(plaintext);
    }

    [Test]
    [Arguments(0)] // Version
    [Arguments(5)] // Nonce
    [Arguments(13)] // Ciphertext
    [Arguments(-1)] // Tag (last byte)
    public async Task DecryptAsync_WithAnyByteAltered_ThrowsCryptographicException(int position)
    {
        // Arrange
        var plaintext = "Transfer $100 to Alice"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("password");
        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);

        // Act: flip one bit, as an attacker editing the database would
        encryptedData[position < 0 ? encryptedData.Length + position : position] ^= 0x01;

        // Assert
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _service.DecryptAsync(encryptedData, metadata, key, _testIterations));
    }

    [Test]
    public async Task DecryptAsync_WithTruncatedData_ThrowsCryptographicException()
    {
        // Arrange
        using var key = EncryptionKey.FromPassphrase("password");
        var (encryptedData, metadata) = await _service.EncryptAsync("data"u8.ToArray(), key, _testIterations);

        // Act & Assert
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _service.DecryptAsync(encryptedData[..^1], metadata, key, _testIterations));

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _service.DecryptAsync(encryptedData[..(EncryptionMetadata.AesGcmOverhead - 1)], metadata, key, _testIterations));
    }

    [Test]
    [Arguments(EncryptionMetadata.LegacyCbcAlgorithm)]
    [Arguments("AES-256-CBC")] // ClipService's old fallback for a clip with no stored method
    public async Task DecryptAsync_LegacyCbcData_StillDecrypts(string algorithm)
    {
        // Arrange: a clip encrypted before AES-GCM
        var plaintext = "Encrypted with ClipMate 1.2"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("legacy-password");
        var (encryptedData, salt, iv) = await EncryptLegacyCbcAsync(plaintext, key);
        var metadata = new EncryptionMetadata(salt, iv, algorithm, 0);

        // Act
        var decryptedData = await _service.DecryptAsync(encryptedData, metadata, key, _testIterations);

        // Assert
        await Assert.That(decryptedData).IsEquivalentTo(plaintext);
    }

    [Test]
    public async Task DecryptAsync_LegacyCbcDataWithWrongKey_ThrowsCryptographicException()
    {
        // Arrange
        using var key = EncryptionKey.FromPassphrase("legacy-password");
        using var wrongKey = EncryptionKey.FromPassphrase("not-the-password");
        var (encryptedData, salt, iv) = await EncryptLegacyCbcAsync("Secret"u8.ToArray(), key);
        var metadata = new EncryptionMetadata(salt, iv, EncryptionMetadata.LegacyCbcAlgorithm, 0);

        // Act & Assert
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _service.DecryptAsync(encryptedData, metadata, wrongKey, _testIterations));
    }

    [Test]
    public async Task DecryptAsync_AesGcmDataReadAsLegacy_ThrowsCryptographicException()
    {
        // Arrange: the method changed to the legacy format, as an attacker might to downgrade the check
        var plaintext = "Sixteen bytes!!!"u8.ToArray();
        using var key = EncryptionKey.FromPassphrase("password");
        var (encryptedData, metadata) = await _service.EncryptAsync(plaintext, key, _testIterations);
        var downgraded = metadata with { Algorithm = EncryptionMetadata.LegacyCbcAlgorithm, IV = Convert.ToBase64String(new byte[16]) };

        // Act & Assert
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _service.DecryptAsync(encryptedData, downgraded, key, _testIterations));
    }

    /// <summary>
    /// Encrypts the way ClipMate did before AES-GCM: AES-256-CBC with an encrypted CRC32 block (IV with its first byte
    /// flipped) in front of the data.
    /// </summary>
    private async Task<(byte[] EncryptedData, string Salt, string Iv)> EncryptLegacyCbcAsync(byte[] data, EncryptionKey key)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(16);
        var derivedKey = await _service.DeriveKeyAsync(key, salt, _testIterations);

        using var aes = Aes.Create();
        aes.Key = derivedKey;

        var checksumIv = (byte[])iv.Clone();
        checksumIv[0] ^= 0xFF;

        var encryptedChecksum = aes.EncryptCbc(BitConverter.GetBytes(Crc32(data)), checksumIv);
        var encryptedData = aes.EncryptCbc(data, iv);

        return ([..encryptedChecksum, ..encryptedData], Convert.ToBase64String(salt), Convert.ToBase64String(iv));
    }

    /// <summary>
    /// CRC-32 (IEEE 802.3), as the legacy format stores it.
    /// </summary>
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var item in data)
        {
            crc ^= item;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) == 1
                    ? crc >> 1 ^ 0xEDB88320
                    : crc >> 1;
            }
        }

        return ~crc;
    }
}
