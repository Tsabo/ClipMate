namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Configuration for encryption features.
/// </summary>
public class EncryptionConfiguration
{
    /// <summary>
    /// Gets or sets the number of PBKDF2 iterations for key derivation.
    /// Higher values are more secure but slower. Minimum: 100,000. Default: 600,000.
    /// </summary>
    public int PbkdfIterations { get; set; } = 600_000;

    /// <summary>
    /// Gets or sets the default key retention time in minutes.
    /// After this time, the encryption key will be automatically forgotten.
    /// Default: 1 minute.
    /// </summary>
    public int DefaultKeyRetentionMinutes { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether to automatically lock (forget encryption key) when the screen is locked.
    /// Default: true.
    /// </summary>
    public bool LockOnScreenLock { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically prompt for decryption when selecting encrypted clips.
    /// Default: true.
    /// </summary>
    public bool AutoPromptForDecryption { get; set; } = true;
}
