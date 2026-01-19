namespace ClipMate.Core.Events;

/// <summary>
/// Event sent when the cached encryption key expires after the retention timeout.
/// This signals that temporarily decrypted clips should be re-locked.
/// </summary>
public record EncryptionKeyExpiredEvent;
