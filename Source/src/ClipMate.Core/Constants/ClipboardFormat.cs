namespace ClipMate.Core.Constants;

/// <summary>
/// Represents a Windows clipboard format with its code and name.
/// </summary>
/// <param name="Code">The numeric format code used by Windows clipboard API.</param>
/// <param name="Name">The string identifier for the format.</param>
public record ClipboardFormat(int Code, string Name);
