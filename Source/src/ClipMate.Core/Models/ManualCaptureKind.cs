namespace ClipMate.Core.Models;

/// <summary>
/// A capture ClipMate starts itself rather than for a clipboard change, each with ClipMate 7's rules.
/// </summary>
public enum ManualCaptureKind
{
    /// <summary>
    /// The Capture hotkey: captures with auto-capture off and from a program whose Application Profile is switched off;
    /// the ignore flag and the profile's Format settings still apply.
    /// </summary>
    CaptureHotkey,

    /// <summary>
    /// The clipboard's content when ClipMate starts: only text or an image, never a change marked to be ignored.
    /// </summary>
    Startup,
}
