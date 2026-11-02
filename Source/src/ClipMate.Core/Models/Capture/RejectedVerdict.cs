namespace ClipMate.Core.Models.Capture;

/// <summary>
/// Keep nothing.
/// </summary>
/// <param name="Reason">Why.</param>
/// <param name="Detail">More about an error, for the Event Log.</param>
public sealed record RejectedVerdict(CaptureRejection Reason, string? Detail = null) : CaptureVerdict;
