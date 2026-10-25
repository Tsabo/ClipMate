using ClipMate.Core.Models;

namespace ClipMate.Core.Events;

/// <summary>
/// Capture Special: capture the chosen Formats of a clipboard reading, regardless of the Application Profile.
/// </summary>
public sealed record CaptureSpecialRequestedEvent(ClipboardReading Reading, IReadOnlyCollection<string> FormatNames);
