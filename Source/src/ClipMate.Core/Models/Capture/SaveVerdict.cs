namespace ClipMate.Core.Models.Capture;

/// <summary>
/// Keep the clip in a collection; the clip store may still find it a Duplicate.
/// </summary>
/// <param name="Clip">The clip to save.</param>
/// <param name="Target">Where it goes.</param>
/// <param name="RejectDuplicate">Whether the Duplicate rule applies.</param>
/// <param name="EndsAppendMode">A copy with no text ends Auto-Append, as in ClipMate 7.</param>
public sealed record SaveVerdict(Clip Clip, CaptureTarget Target, bool RejectDuplicate, bool EndsAppendMode) : CaptureVerdict;
