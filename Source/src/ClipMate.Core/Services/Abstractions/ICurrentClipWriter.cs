using ClipMate.Core.Models;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// The one path for putting a clip on the clipboard (ADR-0004), and the holder of the Current Clip. It loads a stored
/// clip's content, applies the template and the Outbound Filter, and writes it immediately; Power Paste asks it to
/// prepare a clip's Formats and offers them by delayed rendering itself.
/// </summary>
public interface ICurrentClipWriter
{
    /// <summary>
    /// The clip ClipMate last put on, or captured from, the clipboard; null until there is one.
    /// </summary>
    CurrentClip? CurrentClip { get; }

    /// <summary>
    /// Loads the stored clip from its Database, applies the template, puts it on the clipboard and makes it the Current
    /// Clip.
    /// </summary>
    /// <returns>The clip as written, or null when it has no content to write.</returns>
    Task<Clip?> MakeCurrentAsync(string databaseKey, Guid clipId, WriteOptions options = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts content that isn't a stored clip (or one already prepared, such as Power Paste's waiting clip) on the
    /// clipboard as it is, without loading it or applying the template, and makes it the Current Clip.
    /// </summary>
    Task PutOnClipboardAsync(Clip content, WriteOptions options = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// The Formats to offer for a clip with its content loaded, for Power Paste's delayed rendering; the clip becomes the
    /// Current Clip. Call on the UI thread.
    /// </summary>
    IReadOnlyList<ClipboardFormatData> Prepare(Clip clip, WriteOptions options = default);

    /// <summary>
    /// Puts the Current Clip on the clipboard again, loaded from its Database when it is stored: after the Outbound Filter
    /// is turned on or off, or filtered for Manual Filter.
    /// </summary>
    /// <returns>False when there is no Current Clip.</returns>
    Task<bool> RewriteCurrentAsync(WriteOptions options = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Power Paste armed an Explode piece: it becomes the Current Clip without the writer writing it.
    /// </summary>
    void ReportCurrent(Clip clip);

    /// <summary>
    /// Capture saved <paramref name="clip" />, or found it already there as a Duplicate: it becomes the Current Clip, and
    /// with the Outbound Filter on in Active mode it is put back on the clipboard filtered at once.
    /// </summary>
    Task ReportCapturedAsync(Clip clip, string databaseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// ClipMate lost focus: with the Outbound Filter on in Passive mode, a Current Clip captured from another program and
    /// not yet filtered is put back on the clipboard filtered.
    /// </summary>
    /// <returns>True when it was put back.</returns>
    Task<bool> FilterCapturedClipAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// How a write treats the Outbound Filter.
/// </summary>
/// <param name="Filter">Whether the clip is filtered to its plain text, or its image when it has no text.</param>
public readonly record struct WriteOptions(FilterMode Filter = FilterMode.FromSettings);

/// <summary>
/// Whether a write keeps only the plain text, or the image when there is no text.
/// </summary>
public enum FilterMode
{
    /// <summary>
    /// As the Outbound Filter setting says.
    /// </summary>
    FromSettings,

    /// <summary>
    /// Filtered whatever the setting, as Manual Filter does; a clip from an excluded Source Application is still left
    /// whole.
    /// </summary>
    Forced,
}
