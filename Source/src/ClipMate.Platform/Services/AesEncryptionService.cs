using System.Security.Cryptography;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;

namespace ClipMate.Platform.Services;

/// <summary>
/// Implements AES-256-CBC encryption with PBKDF2 key derivation and CRC32 integrity checking.
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private const int _aesKeySize = 256; // AES-256
    private const int _aesBlockSize = 128; // 16 bytes
    private const int _saltSize = 16; // 16 bytes
    private const int _ivSize = 16; // 16 bytes for AES
    private const string _algorithmName = "AES-256";

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

        if (iterations < 100000)
            throw new ArgumentException("PBKDF2 iterations must be at least 100,000.", nameof(iterations));

        // Generate random salt and IV (or reuse if provided)
        byte[] salt;
        byte[] iv;

        if (reuseMetadata != null)
        {
            // Reuse existing salt and IV
            salt = Convert.FromBase64String(reuseMetadata.Salt);
            iv = Convert.FromBase64String(reuseMetadata.IV);

            if (salt.Length != _saltSize)
                throw new ArgumentException($"Reused salt must be {_saltSize} bytes.", nameof(reuseMetadata));

            if (iv.Length != _ivSize)
                throw new ArgumentException($"Reused IV must be {_ivSize} bytes.", nameof(reuseMetadata));
        }
        else
        {
            // Generate random salt and IV
            salt = new byte[_saltSize];
            iv = new byte[_ivSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            rng.GetBytes(iv);
        }

        // Derive encryption key using PBKDF2
        var derivedKey = await DeriveKeyAsync(key, salt, iterations);

        // Calculate CRC32 checksum of original data
        var checksum = CalculateCrc32(data);

        // Encrypt data using AES-256-CBC
        byte[] encryptedData;
        using (var aes = Aes.Create())
        {
            aes.KeySize = _aesKeySize;
            aes.BlockSize = _aesBlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = derivedKey;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            encryptedData = encryptor.TransformFinalBlock(data, 0, data.Length);
        }

        // Encrypt the checksum with the same derived key (different IV for checksum)
        var checksumBytes = BitConverter.GetBytes(checksum);
        var checksumIv = new byte[_ivSize];
        Array.Copy(iv, checksumIv, _ivSize);
        checksumIv[0] ^= 0xFF; // XOR first byte to create different IV

        byte[] encryptedChecksum;
        using (var aes = Aes.Create())
        {
            aes.KeySize = _aesKeySize;
            aes.BlockSize = _aesBlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = derivedKey;
            aes.IV = checksumIv;

            using var encryptor = aes.CreateEncryptor();
            encryptedChecksum = encryptor.TransformFinalBlock(checksumBytes, 0, checksumBytes.Length);
        }

        // Prepend encrypted checksum to encrypted data
        var finalData = new byte[encryptedChecksum.Length + encryptedData.Length];
        Array.Copy(encryptedChecksum, 0, finalData, 0, encryptedChecksum.Length);
        Array.Copy(encryptedData, 0, finalData, encryptedChecksum.Length, encryptedData.Length);

        // Zero out derived key
        Array.Clear(derivedKey, 0, derivedKey.Length);

        var metadata = new EncryptionMetadata(
            Convert.ToBase64String(salt),
            Convert.ToBase64String(iv),
            _algorithmName,
            checksum
        );

        return (finalData, metadata);
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

        if (iterations < 100000)
            throw new ArgumentException("PBKDF2 iterations must be at least 100,000.", nameof(iterations));

        var salt = metadata.GetSaltBytes();
        var iv = metadata.GetIVBytes();

        // Derive encryption key using PBKDF2
        var derivedKey = await DeriveKeyAsync(key, salt, iterations);

        try
        {
            // Extract encrypted checksum (first block) and encrypted data
            var checksumIv = new byte[_ivSize];
            Array.Copy(iv, checksumIv, _ivSize);
            checksumIv[0] ^= 0xFF; // XOR first byte to match encryption

            // Determine encrypted checksum size (at least one AES block)
            const int encryptedChecksumSize = _aesBlockSize / 8; // 16 bytes minimum
            if (encryptedData.Length < encryptedChecksumSize)
                throw new CryptographicException("Encrypted data is too short.");

            var encryptedChecksum = new byte[encryptedChecksumSize];
            Array.Copy(encryptedData, 0, encryptedChecksum, 0, encryptedChecksumSize);

            var actualEncryptedData = new byte[encryptedData.Length - encryptedChecksumSize];
            Array.Copy(encryptedData, encryptedChecksumSize, actualEncryptedData, 0, actualEncryptedData.Length);

            // Decrypt checksum
            byte[] decryptedChecksumBytes;
            using (var aes = Aes.Create())
            {
                aes.KeySize = _aesKeySize;
                aes.BlockSize = _aesBlockSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = derivedKey;
                aes.IV = checksumIv;

                using var decryptor = aes.CreateDecryptor();
                decryptedChecksumBytes = decryptor.TransformFinalBlock(encryptedChecksum, 0, encryptedChecksum.Length);
            }

            var decryptedChecksum = BitConverter.ToUInt32(decryptedChecksumBytes, 0);

            // Decrypt data using AES-256-CBC
            byte[] decryptedData;
            using (var aes = Aes.Create())
            {
                aes.KeySize = _aesKeySize;
                aes.BlockSize = _aesBlockSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = derivedKey;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                decryptedData = decryptor.TransformFinalBlock(actualEncryptedData, 0, actualEncryptedData.Length);
            }

            // Validate checksum
            var actualChecksum = CalculateCrc32(decryptedData);
            return actualChecksum != decryptedChecksum
                ? throw new CryptographicException("Checksum validation failed. Incorrect encryption key or corrupted data.")
                : decryptedData;
        }
        finally
        {
            // Zero out derived key
            Array.Clear(derivedKey, 0, derivedKey.Length);
        }
    }

    /// <inheritdoc />
    public Task<byte[]> DeriveKeyAsync(EncryptionKey key, byte[] salt, int iterations)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(salt);

        if (iterations < 100000)
            throw new ArgumentException("PBKDF2 iterations must be at least 100,000.", nameof(iterations));

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
    /// Calculates CRC32 checksum of data.
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
