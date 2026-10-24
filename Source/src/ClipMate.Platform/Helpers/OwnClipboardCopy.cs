using System.IO;
using ClipMate.Core.Constants;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;

namespace ClipMate.Platform.Helpers;

/// <summary>
/// Copies made by ClipMate itself. Each carries an empty "Clipboard Viewer Ignore" format, as ClipMate 7's copies do, so
/// ClipMate doesn't capture its own copy again; Windows clipboard history still keeps it.
/// </summary>
public static class OwnClipboardCopy
{
    /// <summary>
    /// Adds the "Clipboard Viewer Ignore" format to a data object about to be put on the clipboard.
    /// </summary>
    public static void Mark(DataObject dataObject) =>
        dataObject.SetData(ClipboardIgnoreFormats.ClipboardViewerIgnore, new MemoryStream(), false);

    /// <summary>
    /// The data object a copy of <paramref name="text" /> puts on the clipboard.
    /// </summary>
    public static DataObject CreateText(string text)
    {
        var dataObject = new DataObject(DataFormats.UnicodeText, text);
        Mark(dataObject);

        return dataObject;
    }

    /// <summary>
    /// Puts <paramref name="text" /> on the clipboard as ClipMate's own copy; use instead of <see cref="Clipboard.SetText(string)" />.
    /// Must run on an STA thread.
    /// </summary>
    public static void SetText(string text) => Clipboard.SetDataObject(CreateText(text), true);
}
