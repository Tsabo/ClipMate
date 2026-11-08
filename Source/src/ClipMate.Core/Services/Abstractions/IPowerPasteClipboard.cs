using ClipMate.Core.Models;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// The clipboard as Power Paste uses it: the waiting clip's Formats offered by delayed rendering, so ClipMate hears
/// each time an application reads them. Every member is used on the UI thread, and the events are raised there.
/// </summary>
public interface IPowerPasteClipboard
{
    /// <summary>
    /// Raised when an application reads the offered Formats. It is given all of them afterward, unless a handler sets
    /// <see cref="PowerPasteClipboardReadEventArgs.IsRefused" />.
    /// </summary>
    event EventHandler<PowerPasteClipboardReadEventArgs>? Read;

    /// <summary>
    /// Raised when another program put something on the clipboard in place of the offered Formats.
    /// </summary>
    event EventHandler? Lost;

    /// <summary>
    /// Empties the clipboard and offers these Formats by delayed rendering, taking the clipboard if needed. The offer is
    /// marked so that clipboard history, the cloud clipboard and clipboard managers that respect the markers don't read it.
    /// </summary>
    /// <returns>False when the clipboard could not be opened.</returns>
    bool Offer(IReadOnlyList<ClipboardFormatData> formats);

    /// <summary>
    /// Gives up the clipboard. Formats offered and not yet read are rendered first, so they stay on it.
    /// </summary>
    void Release();
}
