namespace ClipMate.Core.Models;

/// <summary>
/// Record representing a clipboard format request during delayed rendering.
/// </summary>
/// <param name="Timestamp">When the format was requested.</param>
/// <param name="FormatId">The clipboard format ID.</param>
/// <param name="FormatName">The human-readable format name.</param>
/// <param name="DataSize">Size of data provided, or null if not rendered.</param>
/// <param name="RequestingApplication">The name of the application that requested the format.</param>
public record PasteTraceEntry(
    DateTime Timestamp,
    uint FormatId,
    string FormatName,
    long? DataSize,
    string? RequestingApplication = null);
