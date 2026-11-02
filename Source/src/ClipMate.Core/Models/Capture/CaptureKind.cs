namespace ClipMate.Core.Models.Capture;

/// <summary>
/// How a capture was made, which decides the rules it skips (ClipMate 7's capture handler).
/// </summary>
public enum CaptureKind
{
    /// <summary>
    /// A clipboard change ClipMate noticed while Auto Capture is on.
    /// </summary>
    Monitored,

    /// <summary>
    /// The Capture hotkey: never a Duplicate.
    /// </summary>
    CaptureHotkey,

    /// <summary>
    /// The clipboard as it was when ClipMate started.
    /// </summary>
    Startup,

    /// <summary>
    /// Capture Special, with the Formats the user chose: never a Duplicate, never a small graphic.
    /// </summary>
    CaptureSpecial,
}
