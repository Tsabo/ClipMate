namespace ClipMate.Core.Models;

/// <summary>
/// One clipboard format's data exactly as it is placed on the clipboard (the content of its global memory block).
/// </summary>
/// <param name="FormatId">The clipboard format identifier.</param>
/// <param name="Data">The format's bytes.</param>
public record ClipboardFormatData(uint FormatId, byte[] Data);
