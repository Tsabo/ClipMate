using ClipMate.Core.ValueObjects;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Service for encrypting and decrypting clip content.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts data using AES-256-GCM with the provided key and a fresh random salt and nonce.
    /// </summary>
    /// <param name="data">The plaintext data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iterations">PBKDF2 iteration count for key derivation.</param>
    /// <returns>Encrypted data and metadata (salt and algorithm).</returns>
    Task<(byte[] EncryptedData, EncryptionMetadata Metadata)> EncryptAsync(byte[] data,
        EncryptionKey key,
        int iterations);

    /// <summary>
    /// Encrypts data using AES-256-GCM with the provided key, optionally reusing the salt of existing metadata.
    /// </summary>
    /// <param name="data">The plaintext data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iterations">PBKDF2 iteration count for key derivation.</param>
    /// <param name="reuseMetadata">
    /// Optional metadata whose salt to reuse, so every field of a clip shares the clip's one stored salt.
    /// If null, a new random salt is generated. A new random nonce is generated for every call either way.
    /// </param>
    /// <returns>Encrypted data and metadata (salt and algorithm).</returns>
    Task<(byte[] EncryptedData, EncryptionMetadata Metadata)> EncryptAsync(byte[] data,
        EncryptionKey key,
        int iterations,
        EncryptionMetadata? reuseMetadata);

    /// <summary>
    /// Decrypts data with the provided key: AES-256-GCM, or the legacy AES-256-CBC format, chosen by
    /// <see cref="EncryptionMetadata.Algorithm" />.
    /// </summary>
    /// <param name="encryptedData">The encrypted data.</param>
    /// <param name="metadata">Encryption metadata (salt, algorithm, and the IV for the legacy format).</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iterations">PBKDF2 iteration count for key derivation.</param>
    /// <returns>Decrypted plaintext data.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown if decryption or authentication fails: the key is wrong or the data was altered.
    /// </exception>
    Task<byte[]> DecryptAsync(byte[] encryptedData,
        EncryptionMetadata metadata,
        EncryptionKey key,
        int iterations);
}
