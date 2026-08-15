namespace ClipMate.Core.Services;

/// <summary>
/// Tracks the in-memory state of Auto-Append mode: whether it is active, and which clip
/// newly captured text is currently being merged into.
/// </summary>
public interface IAutoAppendService
{
    /// <summary>
    /// Whether Auto-Append mode is currently active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// The clip that new captures are being appended onto, or null if no capture has
    /// occurred yet since Auto-Append mode was activated.
    /// </summary>
    Guid? GrowingClipId { get; }

    /// <summary>
    /// The database key the growing clip belongs to.
    /// </summary>
    string? DatabaseKey { get; }

    /// <summary>
    /// Activates Auto-Append mode. The next captured clip becomes the growing clip.
    /// </summary>
    void Activate();

    /// <summary>
    /// Deactivates Auto-Append mode and clears the growing clip.
    /// </summary>
    void Deactivate();

    /// <summary>
    /// Marks the given clip as the growing clip that subsequent captures will be appended onto.
    /// </summary>
    void SetGrowingClip(Guid clipId, string databaseKey);
}
