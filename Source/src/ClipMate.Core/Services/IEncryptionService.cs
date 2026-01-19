using ClipMate.Core.ValueObjects;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for encrypting and decrypting clip content.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts data using AES-256-CBC with the provided key.
    /// </summary>
    /// <param name="data">The plaintext data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iterations">PBKDF2 iteration count for key derivation.</param>
    /// <returns>Encrypted data and metadata (salt, IV, checksum).</returns>
    Task<(byte[] EncryptedData, EncryptionMetadata Metadata)> EncryptAsync(byte[] data,
        EncryptionKey key,
        int iterations);

    /// <summary>
    /// Encrypts data using AES-256-CBC with the provided key, optionally reusing existing metadata.
    /// </summary>
    /// <param name="data">The plaintext data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iterations">PBKDF2 iteration count for key derivation.</param>
    /// <param name="reuseMetadata">
    /// Optional metadata (salt, IV) to reuse for encryption.
    /// If provided, the same salt and IV will be used. If null, new random salt and IV will be generated.
    /// </param>
    /// <returns>Encrypted data and metadata (salt, IV, checksum).</returns>
    Task<(byte[] EncryptedData, EncryptionMetadata Metadata)> EncryptAsync(byte[] data,
        EncryptionKey key,
        int iterations,
        EncryptionMetadata? reuseMetadata);

    /// <summary>
    /// Decrypts data using AES-256-CBC with the provided key.
    /// </summary>
    /// <param name="encryptedData">The encrypted data.</param>
    /// <param name="metadata">Encryption metadata (salt, IV, checksum).</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iterations">PBKDF2 iteration count for key derivation.</param>
    /// <returns>Decrypted plaintext data.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown if decryption fails or checksum validation fails.
    /// </exception>
    Task<byte[]> DecryptAsync(byte[] encryptedData,
        EncryptionMetadata metadata,
        EncryptionKey key,
        int iterations);

    /// <summary>
    /// Derives a cryptographic key from a passphrase using PBKDF2.
    /// </summary>
    /// <param name="key">The encryption key.</param>
    /// <param name="salt">Salt for key derivation.</param>
    /// <param name="iterations">PBKDF2 iteration count.</param>
    /// <returns>Derived key bytes (32 bytes for AES-256).</returns>
    Task<byte[]> DeriveKeyAsync(EncryptionKey key, byte[] salt, int iterations);
}
