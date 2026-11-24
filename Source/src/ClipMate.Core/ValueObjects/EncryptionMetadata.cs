namespace ClipMate.Core.ValueObjects;

/// <summary>
/// Contains encryption metadata for a clip (salt, IV, algorithm, checksum).
/// </summary>
/// <param name="Salt">Base64-encoded PBKDF2 salt (16 bytes), shared by every field of a clip.</param>
/// <param name="IV">
/// Base64-encoded initialization vector (16 bytes) for <see cref="LegacyCbcAlgorithm" />; empty for
/// <see cref="AesGcmAlgorithm" />, which stores a fresh nonce inside each field's ciphertext.
/// </param>
/// <param name="Algorithm">Encryption algorithm name: <see cref="AesGcmAlgorithm" /> or <see cref="LegacyCbcAlgorithm" />.</param>
/// <param name="Checksum">CRC32 checksum of original data for <see cref="LegacyCbcAlgorithm" />; 0 otherwise.</param>
public record EncryptionMetadata(
    string Salt,
    string IV,
    string Algorithm,
    uint Checksum)
{
    /// <summary>
    /// AES-256-GCM (authenticated): each field is <c>version ‖ 12-byte random nonce ‖ ciphertext ‖ 16-byte tag</c>.
    /// Used for everything encrypted from now on.
    /// </summary>
    public const string AesGcmAlgorithm = "AES-256-GCM";

    /// <summary>
    /// The first byte of every <see cref="AesGcmAlgorithm" /> field.
    /// </summary>
    public const byte AesGcmFormatVersion = 2;

    /// <summary>
    /// Bytes an <see cref="AesGcmAlgorithm" /> field adds to its plaintext: version, 12-byte nonce and 16-byte tag.
    /// </summary>
    public const int AesGcmOverhead = 1 + 12 + 16;

    /// <summary>
    /// The original AES-256-CBC + CRC32 format, one IV shared by every field of a clip. Only decrypted, never written.
    /// Clips encrypted before <see cref="AesGcmAlgorithm" /> store "AES-256" (or nothing) as their method.
    /// </summary>
    public const string LegacyCbcAlgorithm = "AES-256";

    /// <summary>
    /// True when the data was written with <see cref="AesGcmAlgorithm" />.
    /// </summary>
    public bool IsAuthenticated => string.Equals(Algorithm, AesGcmAlgorithm, StringComparison.Ordinal);

    /// <summary>
    /// Creates encryption metadata from Base64-encoded values.
    /// </summary>
    public static EncryptionMetadata FromBase64(string salt, string iv, string algorithm, uint checksum)
    {
        if (string.IsNullOrWhiteSpace(salt))
            throw new ArgumentException("Salt cannot be null or empty.", nameof(salt));

        if (string.IsNullOrWhiteSpace(algorithm))
            throw new ArgumentException("Algorithm cannot be null or empty.", nameof(algorithm));

        // Only the legacy CBC format has a per-clip IV; AES-GCM keeps a nonce in each field
        if (string.IsNullOrWhiteSpace(iv) && !string.Equals(algorithm, AesGcmAlgorithm, StringComparison.Ordinal))
            throw new ArgumentException("IV cannot be null or empty.", nameof(iv));

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
