namespace ClipMate.Core.Models.Capture;

/// <summary>
/// Why a capture was rejected.
/// </summary>
public enum CaptureRejection
{
    /// <summary>
    /// Its Application Profile left no acceptable Formats.
    /// </summary>
    NoAcceptableFormats,

    /// <summary>
    /// A small image from Windows, probably a toolbar button being copied.
    /// </summary>
    SmallGraphic,

    /// <summary>
    /// Something went wrong choosing where it goes.
    /// </summary>
    Error,
}
