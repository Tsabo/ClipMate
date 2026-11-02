namespace ClipMate.Core.Models.Capture;

/// <summary>
/// Add the captured text to the growing clip of Auto-Append.
/// </summary>
/// <param name="DatabaseKey">The growing clip's Database.</param>
/// <param name="GrowingClipId">The growing clip.</param>
/// <param name="Text">The captured plain text.</param>
public sealed record AppendVerdict(string DatabaseKey, Guid GrowingClipId, string Text) : CaptureVerdict;
