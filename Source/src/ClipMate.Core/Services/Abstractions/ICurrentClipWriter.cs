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
    /// A clip became the Current Clip without the writer writing it: capture saved it or found it already there as a
    /// Duplicate, or Power Paste armed an Explode piece.
    /// </summary>
    /// <param name="clip">The clip.</param>
    /// <param name="databaseKey">The Database it is stored in; null for content that isn't a stored clip.</param>
    void ReportCurrent(Clip clip, string? databaseKey);
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
    /// Always filtered, as Manual Filter does.
    /// </summary>
    Forced,
}
