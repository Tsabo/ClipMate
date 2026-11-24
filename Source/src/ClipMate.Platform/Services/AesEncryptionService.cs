using System.Security.Cryptography;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;

namespace ClipMate.Platform.Services;

/// <summary>
/// Encrypts with AES-256-GCM (authenticated encryption) and a PBKDF2-derived key. Every call uses a fresh random
/// nonce, so fields that share a clip's salt never share a nonce. Data written by the original AES-256-CBC + CRC32
/// format is still decrypted, selected by <see cref="EncryptionMetadata.Algorithm" />.
/// </summary>
/// <remarks>
/// AES-GCM field layout: <c>version (1 byte) ‖ nonce (12 bytes) ‖ ciphertext ‖ tag (16 bytes)</c>. The version byte
/// is also the associated data, so it is authenticated with the ciphertext.
/// </remarks>
public sealed class AesEncryptionService : IEncryptionService
{
    private const int _aesKeySize = 256; // AES-256
    private const int _aesBlockSize = 128; // 16 bytes
    private const int _saltSize = 16; // 16 bytes
    private const int _ivSize = 16; // 16 bytes for AES-CBC
    private const int _minimumIterations = 100_000;

    private const byte _gcmFormatVersion = EncryptionMetadata.AesGcmFormatVersion;
    private const int _gcmNonceSize = 12; // AesGcm.NonceByteSizes.MaxSize
    private const int _gcmTagSize = 16; // AesGcm.TagByteSizes.MaxSize
    private const int _gcmOverhead = EncryptionMetadata.AesGcmOverhead; // version + nonce + tag

    /// <inheritdoc />
    public async Task<(byte[] EncryptedData, EncryptionMetadata Metadata)> EncryptAsync(byte[] data,
        EncryptionKey key,
        int iterations) =>
        await EncryptAsync(data, key, iterations, null);

    /// <inheritdoc />
    public async Task<(byte[] EncryptedData, EncryptionMetadata Metadata)> EncryptAsync(byte[] data,
        EncryptionKey key,
        int iterations,
        EncryptionMetadata? reuseMetadata)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(key);
        ValidateIterations(iterations);

        // The salt may be shared by the fields of one clip (one stored salt per clip); the nonce never is
        byte[] salt;
        if (reuseMetadata != null)
        {
            salt = reuseMetadata.GetSaltBytes();
            if (salt.Length != _saltSize)
                throw new ArgumentException($"Reused salt must be {_saltSize} bytes.", nameof(reuseMetadata));
        }
        else
            salt = RandomNumberGenerator.GetBytes(_saltSize);

        var derivedKey = await DeriveKeyAsync(key, salt, iterations);
        try
        {
            var output = EncryptGcm(data, derivedKey);

            var metadata = new EncryptionMetadata(
                Convert.ToBase64String(salt),
                string.Empty,
                EncryptionMetadata.AesGcmAlgorithm,
                0);

            return (output, metadata);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> DecryptAsync(byte[] encryptedData,
        EncryptionMetadata metadata,
        EncryptionKey key,
        int iterations)
    {
        ArgumentNullException.ThrowIfNull(encryptedData);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(key);
        ValidateIterations(iterations);

        var derivedKey = await DeriveKeyAsync(key, metadata.GetSaltBytes(), iterations);
        try
        {
            return metadata.IsAuthenticated
                ? DecryptGcm(encryptedData, derivedKey)
                : DecryptLegacyCbc(encryptedData, metadata.GetIVBytes(), derivedKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    /// <summary>
    /// Encrypts one AES-GCM field with a fresh random nonce.
    /// </summary>
    private static byte[] EncryptGcm(byte[] data, byte[] derivedKey)
    {
        var output = new byte[_gcmOverhead + data.Length];
        output[0] = _gcmFormatVersion;

        var nonce = output.AsSpan(1, _gcmNonceSize);
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = output.AsSpan(1 + _gcmNonceSize, data.Length);
        var tag = output.AsSpan(1 + _gcmNonceSize + data.Length, _gcmTagSize);

        using var aes = new AesGcm(derivedKey, _gcmTagSize);
        aes.Encrypt(nonce, data, ciphertext, tag, output.AsSpan(0, 1));

        return output;
    }

    /// <summary>
    /// Decrypts and authenticates one AES-GCM field.
    /// </summary>
    /// <exception cref="CryptographicException">The data is malformed, the key is wrong, or the data was altered.</exception>
    private static byte[] DecryptGcm(byte[] encryptedData, byte[] derivedKey)
    {
        if (encryptedData.Length < _gcmOverhead)
            throw new CryptographicException("Encrypted data is too short.");

        if (encryptedData[0] != _gcmFormatVersion)
            throw new CryptographicException($"Unsupported encryption format version {encryptedData[0]}.");

        var plaintextLength = encryptedData.Length - _gcmOverhead;
        var nonce = encryptedData.AsSpan(1, _gcmNonceSize);
        var ciphertext = encryptedData.AsSpan(1 + _gcmNonceSize, plaintextLength);
        var tag = encryptedData.AsSpan(1 + _gcmNonceSize + plaintextLength, _gcmTagSize);

        var plaintext = new byte[plaintextLength];
        try
        {
            using var aes = new AesGcm(derivedKey, _gcmTagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, encryptedData.AsSpan(0, 1));
        }
        catch (CryptographicException ex)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new CryptographicException("Authentication failed. Incorrect encryption key or the data was altered.", ex);
        }

        return plaintext;
    }

    /// <summary>
    /// Decrypts the original AES-256-CBC format: an encrypted CRC32 block (IV with its first byte flipped) followed by
    /// the encrypted data (the clip's IV). Kept only so clips encrypted before AES-GCM can still be opened.
    /// </summary>
    private static byte[] DecryptLegacyCbc(byte[] encryptedData, byte[] iv, byte[] derivedKey)
    {
        if (iv.Length != _ivSize)
            throw new CryptographicException($"IV must be {_ivSize} bytes.");

        var checksumIv = (byte[])iv.Clone();
        checksumIv[0] ^= 0xFF;

        const int encryptedChecksumSize = _aesBlockSize / 8; // one AES block
        if (encryptedData.Length < encryptedChecksumSize)
            throw new CryptographicException("Encrypted data is too short.");

        using var aes = Aes.Create();
        aes.KeySize = _aesKeySize;
        aes.Key = derivedKey;

        var checksumBytes = aes.DecryptCbc(encryptedData.AsSpan(0, encryptedChecksumSize), checksumIv, PaddingMode.PKCS7);
        if (checksumBytes.Length < sizeof(uint))
            throw new CryptographicException("Checksum validation failed. Incorrect encryption key or corrupted data.");

        var expectedChecksum = BitConverter.ToUInt32(checksumBytes, 0);
        var decryptedData = aes.DecryptCbc(encryptedData.AsSpan(encryptedChecksumSize), iv, PaddingMode.PKCS7);

        return CalculateCrc32(decryptedData) != expectedChecksum
            ? throw new CryptographicException("Checksum validation failed. Incorrect encryption key or corrupted data.")
            : decryptedData;
    }

    private static void ValidateIterations(int iterations)
    {
        if (iterations < _minimumIterations)
            throw new ArgumentException("PBKDF2 iterations must be at least 100,000.", nameof(iterations));
    }

    /// <summary>
    /// Derives a cryptographic key from a passphrase using PBKDF2.
    /// </summary>
    /// <param name="key">The encryption key.</param>
    /// <param name="salt">Salt for key derivation.</param>
    /// <param name="iterations">PBKDF2 iteration count.</param>
    /// <returns>Derived key bytes (32 bytes for AES-256).</returns>
    public Task<byte[]> DeriveKeyAsync(EncryptionKey key, byte[] salt, int iterations)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(salt);

        ValidateIterations(iterations);

        return Task.Run(() =>
        {
            var keyBytes = key.GetKeyBytes();
            return Rfc2898DeriveBytes.Pbkdf2(
                keyBytes,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                _aesKeySize / 8); // 32 bytes for AES-256
        });
    }

    /// <summary>
    /// Calculates CRC32 checksum of data (legacy CBC format only).
    /// </summary>
    private static uint CalculateCrc32(byte[] data)
    {
        const uint polynomial = 0xEDB88320;
        var table = new uint[256];

        // Build CRC32 table
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) == 1
                    ? crc >> 1 ^ polynomial
                    : crc >> 1;
            }

            table[i] = crc;
        }

        // Calculate CRC32
        var result = 0xFFFFFFFF;
        foreach (var item in data)
        {
            var index = (result ^ item) & 0xFF;
            result = result >> 8 ^ table[index];
        }

        return ~result;
    }
}
