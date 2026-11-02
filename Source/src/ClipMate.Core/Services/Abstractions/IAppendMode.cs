namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Auto-Append's state: whether it is on, and the growing clip each captured text is added to. The Capture Policy
/// reads it; the Append command and the capture coordinator change it (ADR-0005).
/// </summary>
public interface IAppendMode
{
    /// <summary>
    /// Whether Auto-Append is on.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// The growing clip, while Auto-Append is on.
    /// </summary>
    Guid? GrowingClipId { get; }

    /// <summary>
    /// The growing clip's Database.
    /// </summary>
    string? DatabaseKey { get; }

    /// <summary>
    /// Turns Auto-Append on, adding captured text to <paramref name="growingClipId" />.
    /// </summary>
    void Start(Guid growingClipId, string databaseKey);

    /// <summary>
    /// Turns Auto-Append off.
    /// </summary>
    void Stop();
}
