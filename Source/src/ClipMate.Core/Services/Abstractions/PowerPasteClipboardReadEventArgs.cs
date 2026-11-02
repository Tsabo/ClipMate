namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// An application read the Formats Power Paste offered.
/// </summary>
public class PowerPasteClipboardReadEventArgs : EventArgs
{
    /// <summary>
    /// True when the reader is the application the user is working in, so the user pasted; false for clipboard history,
    /// another clipboard manager or ClipMate itself.
    /// </summary>
    public bool IsPaste { get; init; }

    /// <summary>
    /// The reading process, for the log.
    /// </summary>
    public string ReaderName { get; init; } = string.Empty;
}
