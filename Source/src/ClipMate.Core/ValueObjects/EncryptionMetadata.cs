namespace ClipMate.Core.ValueObjects;

/// <summary>
/// Contains encryption metadata for a clip (salt, IV, algorithm, checksum).
/// </summary>
/// <param name="Salt">Base64-encoded salt (16 bytes).</param>
/// <param name="IV">Base64-encoded initialization vector (16 bytes for AES).</param>
/// <param name="Algorithm">Encryption algorithm name (e.g., "AES-256").</param>
/// <param name="Checksum">CRC32 checksum of original data (encrypted with same key).</param>
public record EncryptionMetadata(
    string Salt,
    string IV,
    string Algorithm,
    uint Checksum)
{
    /// <summary>
    /// Creates encryption metadata from Base64-encoded values.
    /// </summary>
    public static EncryptionMetadata FromBase64(string salt, string iv, string algorithm, uint checksum)
    {
        if (string.IsNullOrWhiteSpace(salt))
            throw new ArgumentException("Salt cannot be null or empty.", nameof(salt));

        if (string.IsNullOrWhiteSpace(iv))
            throw new ArgumentException("IV cannot be null or empty.", nameof(iv));

        if (string.IsNullOrWhiteSpace(algorithm))
            throw new ArgumentException("Algorithm cannot be null or empty.", nameof(algorithm));

        return new EncryptionMetadata(salt, iv, algorithm, checksum);
    }

    /// <summary>
    /// Gets the salt as byte array.
    /// </summary>
    public byte[] GetSaltBytes() => Convert.FromBase64String(Salt);

    /// <summary>
    /// Gets the IV as byte array.
    /// </summary>
    public byte[] GetIVBytes() => Convert.FromBase64String(IV);
}
