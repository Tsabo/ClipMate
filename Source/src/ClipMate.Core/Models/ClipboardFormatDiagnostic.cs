namespace ClipMate.Core.Models;

/// <summary>
/// Represents diagnostic information about a clipboard format.
/// </summary>
/// <param name="FormatId">The clipboard format identifier.</param>
/// <param name="FormatName">The human-readable format name.</param>
/// <param name="DataSize">The size of the data in bytes, or null if unavailable.</param>
public record ClipboardFormatDiagnostic(uint FormatId, string FormatName, long? DataSize);
