namespace ClipMate.Core.Models.Capture;

/// <summary>
/// What a clipboard change offers the Capture Policy (ADR-0005).
/// </summary>
/// <param name="Clip">The decoded clip; null when its Application Profile left no acceptable Formats.</param>
/// <param name="PlainText">The clipboard's plain text, read before the Application Profile, for Auto-Append.</param>
/// <param name="SourceApplication">The Source Application's program name.</param>
/// <param name="Kind">How the capture was made.</param>
/// <param name="LargestFormatSize">The size in bytes of the largest Format offered, for the small-graphic rule.</param>
public sealed record CaptureAttempt(Clip? Clip, string? PlainText, string? SourceApplication, CaptureKind Kind, int LargestFormatSize);
