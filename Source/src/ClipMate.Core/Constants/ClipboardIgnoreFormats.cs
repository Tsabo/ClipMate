namespace ClipMate.Core.Constants;

/// <summary>
/// Formats a program adds to a clipboard change to ask clipboard viewers and managers not to record it.
/// </summary>
public static class ClipboardIgnoreFormats
{
    /// <summary>
    /// The classic clipboard viewer ignore flag, which ClipMate 7 honours and sets.
    /// </summary>
    public const string ClipboardViewerIgnore = "Clipboard Viewer Ignore";

    /// <summary>
    /// Keeps the change out of the Windows clipboard history and cloud clipboard.
    /// </summary>
    public const string ExcludeFromMonitorProcessing = "ExcludeClipboardContentFromMonitorProcessing";

    /// <summary>
    /// With a DWORD 0, keeps the change out of the Windows clipboard history.
    /// </summary>
    public const string CanIncludeInClipboardHistory = "CanIncludeInClipboardHistory";

    /// <summary>
    /// With a DWORD 0, keeps the change out of the cloud clipboard.
    /// </summary>
    public const string CanUploadToCloudClipboard = "CanUploadToCloudClipboard";

    public static readonly IReadOnlyList<string> All = [ClipboardViewerIgnore, ExcludeFromMonitorProcessing];
}
