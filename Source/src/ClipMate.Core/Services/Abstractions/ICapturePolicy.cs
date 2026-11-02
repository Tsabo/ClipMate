using ClipMate.Core.Models.Capture;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Decides what happens to a capture, in ClipMate 7's order (ADR-0005): Auto-Append, the Application Profile's
/// result, the target collection, the small-graphic rule; the clip store then applies Duplicate. It has no side effects.
/// </summary>
public interface ICapturePolicy
{
    /// <summary>
    /// The verdict for <paramref name="attempt" />.
    /// </summary>
    Task<CaptureVerdict> DecideAsync(CaptureAttempt attempt, CancellationToken cancellationToken = default);
}
