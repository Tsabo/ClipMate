namespace ClipMate.Platform;

/// <summary>
/// Represents information about a clipboard format.
/// </summary>
public record ClipboardFormatInfo(string FormatName, uint FormatCode);

/// <summary>
/// Defines a service for enumerating clipboard formats.
/// Provides a testable abstraction over Win32 clipboard format enumeration APIs.
/// </summary>
public interface IClipboardFormatEnumerator
{
    /// <summary>
    /// Gets all available clipboard formats currently on the clipboard.
    /// </summary>
    /// <returns>A collection of clipboard format information including format names and codes.</returns>
    /// <remarks>
    /// This method enumerates all formats on the clipboard using EnumClipboardFormats
    /// and retrieves the format names using GetClipboardFormatNameW.
    /// Standard formats (CF_TEXT, CF_BITMAP, etc.) have fixed codes and names.
    /// Custom registered formats have dynamic codes but stable names.
    /// </remarks>
    IReadOnlyList<ClipboardFormatInfo> GetAllAvailableFormats();
}
