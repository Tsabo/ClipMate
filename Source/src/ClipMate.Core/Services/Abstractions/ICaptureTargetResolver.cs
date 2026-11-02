using ClipMate.Core.Models.Capture;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Where a new capture goes: the active collection, or after the bounce the first collection that accepts new clips,
/// or failing that the first collection, so a capture is never refused for its collection (ClipMate 7).
/// </summary>
public interface ICaptureTargetResolver
{
    /// <summary>
    /// The target; null when no Database is active.
    /// </summary>
    Task<CaptureTarget?> ResolveAsync(CancellationToken cancellationToken = default);
}
