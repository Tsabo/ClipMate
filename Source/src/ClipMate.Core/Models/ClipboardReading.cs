namespace ClipMate.Core.Models;

/// <summary>
/// What one read of the clipboard found, for Capture Special to choose from: the Formats in the source's order and the
/// Source Application.
/// </summary>
public sealed record ClipboardReading(IReadOnlyList<RawClipboardFormat> Formats, string? SourceApplicationName, string? SourceApplicationTitle);
