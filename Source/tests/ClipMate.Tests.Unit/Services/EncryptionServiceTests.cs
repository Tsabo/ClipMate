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
        await Assert.That(metadata.Algorithm).IsEqualTo("AES-256");
        await Assert.That(metadata.Salt).IsNotEmpty();
        await Assert.That(metadata.IV).IsNotEmpty();
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
        await Assert.That(encryptedData.Length).IsGreaterThan(0); // Still has checksum + padding
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

        // Assert
        await Assert.That(decryptedData).IsEquivalentTo(plaintext);
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

        // Act & Assert - Should throw due to checksum mismatch
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _service.DecryptAsync(encryptedData, metadata, wrongKey, _testIterations));
    }
}
