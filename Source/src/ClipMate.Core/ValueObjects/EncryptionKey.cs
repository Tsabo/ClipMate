using System.Text;

namespace ClipMate.Core.ValueObjects;

/// <summary>
/// Represents an encryption key with secure memory management.
/// Implements IDisposable to ensure memory is zeroed when no longer needed.
/// </summary>
public sealed class EncryptionKey : IDisposable
{
    private byte[]? _keyBytes;

    private EncryptionKey(byte[] keyBytes, int? expirationMinutes = null)
    {
        _keyBytes = keyBytes;
        ExpirationMinutes = expirationMinutes;
    }

    /// <summary>
    /// Gets whether the key has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the expiration time in minutes for cached decrypted data.
    /// Null means "until shutdown" (no expiration).
    /// </summary>
    public int? ExpirationMinutes { get; }

    /// <summary>
    /// Disposes the key and zeros the memory.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;

        // Zero out the key bytes in memory
        if (_keyBytes != null)
        {
            Array.Clear(_keyBytes, 0, _keyBytes.Length);
            _keyBytes = null;
        }

        IsDisposed = true;
    }

    /// <summary>
    /// Creates an encryption key from a passphrase.
    /// </summary>
    /// <param name="passphrase">The passphrase to create the key from.</param>
    /// <param name="expirationMinutes">Optional expiration time in minutes for cached data. Null means "until shutdown".</param>
    /// <returns>A new EncryptionKey instance.</returns>
    /// <exception cref="ArgumentException">Thrown if passphrase is null, empty, or too weak.</exception>
    public static EncryptionKey FromPassphrase(string passphrase, int? expirationMinutes = null)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("Passphrase cannot be null or empty.", nameof(passphrase));

        if (passphrase.Length < 4)
            throw new ArgumentException("Passphrase must be at least 4 characters long.", nameof(passphrase));

        // Check for overly simplistic keys with repeating characters
        if (IsRepeatingCharacters(passphrase))
            throw new ArgumentException("Passphrase cannot consist of repeating characters (e.g., 'aaaa').", nameof(passphrase));

        // Convert passphrase to UTF-8 bytes
        var keyBytes = Encoding.UTF8.GetBytes(passphrase);
        return new EncryptionKey(keyBytes, expirationMinutes);
    }

    /// <summary>
    /// Gets the key bytes. Throws if disposed.
    /// </summary>
    public byte[] GetKeyBytes()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return _keyBytes ?? throw new InvalidOperationException("Key bytes are null.");
    }

    /// <summary>
    /// Checks if a string consists only of repeating characters.
    /// </summary>
    private static bool IsRepeatingCharacters(string value)
    {
        if (value.Length < 2)
            return false;

        var firstChar = value[0];
        return value.All(p => p == firstChar);
    }
}
