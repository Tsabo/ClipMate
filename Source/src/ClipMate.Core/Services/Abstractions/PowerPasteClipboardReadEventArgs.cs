namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// An application read the Formats Power Paste offered.
/// </summary>
public class PowerPasteClipboardReadEventArgs : EventArgs
{
    /// <summary>
    /// True when the window that read belongs to the application in the foreground, which is not ClipMate.
    /// </summary>
    public bool IsByForegroundApplication { get; init; }

    /// <summary>
    /// True when the read cannot be tied to a process, so it may be the application in the foreground pasting: it came
    /// without a window, as browsers, Electron programs and Windows Terminal read, or a Store app is in the foreground,
    /// whose reads come from a Windows service.
    /// </summary>
    public bool MayBeForForegroundApplication { get; init; }

    /// <summary>
    /// True when ClipMate itself read.
    /// </summary>
    public bool IsByClipMate { get; init; }

    /// <summary>
    /// True when ClipMate is in the foreground, so the user is not pasting into another application.
    /// </summary>
    public bool IsClipMateInForeground { get; init; }

    /// <summary>
    /// How long ago the user last pressed a key or used the mouse, in any application.
    /// </summary>
    public TimeSpan SinceUserInput { get; init; }

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
