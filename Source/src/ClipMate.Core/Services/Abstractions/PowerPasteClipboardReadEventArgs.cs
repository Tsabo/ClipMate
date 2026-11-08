namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// An application read the Formats Power Paste offered: who read, and what was in the foreground.
/// </summary>
public class PowerPasteClipboardReadEventArgs : EventArgs
{
    /// <summary>
    /// True when the window that read belongs to the application in the foreground, which is not ClipMate.
    /// </summary>
    public bool IsByForegroundApplication { get; init; }

    /// <summary>
    /// True when the clipboard was opened without a window, as browsers, Electron programs and Windows Terminal open it.
    /// </summary>
    public bool IsWithoutWindow { get; init; }

    /// <summary>
    /// True when a Store app is in the foreground. Its reads come from a Windows service, so they can't be tied to it.
    /// </summary>
    public bool IsStoreAppInForeground { get; init; }

    /// <summary>
    /// True when ClipMate itself read.
    /// </summary>
    public bool IsByClipMate { get; init; }

    /// <summary>
    /// True when ClipMate is in the foreground, so the user is not pasting into another application.
    /// </summary>
    public bool IsClipMateInForeground { get; init; }

    /// <summary>
    /// The reading process, for the log.
    /// </summary>
    public string ReaderName { get; init; } = string.Empty;

    /// <summary>
    /// Set by a handler to give the reader nothing. The Formats stay offered by delayed rendering, so the clipboard does
    /// not change and the next read is still heard.
    /// </summary>
    public bool IsRefused { get; set; }
}
