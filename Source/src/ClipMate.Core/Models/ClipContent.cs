using System.Text;
using ClipboardFormats = ClipMate.Core.Constants.Formats;

namespace ClipMate.Core.Models;

/// <summary>
/// A clip with all its formats and their stored content, as read from its Database (local or remote).
/// <para>
/// The content is exactly what the Database stores: for an encrypted clip it is still ciphertext, and it is
/// decrypted only on this Device (a Peer never decrypts on anyone's behalf). The objects are detached copies, so
/// changing them never changes the Database.
/// </para>
/// </summary>
/// <param name="Clip">The clip's metadata.</param>
/// <param name="Formats">The clip's formats (one ClipData entry per clipboard format).</param>
/// <param name="TextBlobs">Content of the formats stored as text.</param>
/// <param name="JpgBlobs">Content of the formats stored as JPEG images.</param>
/// <param name="PngBlobs">Content of the formats stored as PNG images.</param>
/// <param name="BinaryBlobs">Content of the formats stored as binary data.</param>
public sealed record ClipContent(
    Clip Clip,
    IReadOnlyList<ClipData> Formats,
    IReadOnlyList<BlobTxt> TextBlobs,
    IReadOnlyList<BlobJpg> JpgBlobs,
    IReadOnlyList<BlobPng> PngBlobs,
    IReadOnlyList<BlobBlob> BinaryBlobs)
{
    /// <summary>
    /// Fills the transient content properties of <paramref name="target" /> (text, RTF, HTML, image and file list)
    /// from the stored formats, the way the clipboard and paste operations expect them.
    /// </summary>
    public void PopulateContent(Clip target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var textBlobs = TextBlobs.GroupBy(p => p.ClipDataId).ToDictionary(p => p.Key, p => p.First());
        foreach (var item in Formats)
        {
            if (!textBlobs.TryGetValue(item.Id, out var textBlob))
                continue;

            if (item.Format == ClipboardFormats.Text.Code || item.Format == ClipboardFormats.UnicodeText.Code)
                target.TextContent = textBlob.Data;
            else if (item.Format == ClipboardFormats.RichText.Code)
                target.RtfContent = textBlob.Data;
            else if (item.Format == ClipboardFormats.Html.Code || item.Format == ClipboardFormats.HtmlAlt.Code)
                target.HtmlContent = textBlob.Data;
        }

        if (target.Type == ClipType.Image)
        {
            var image = PngBlobs.Select(p => p.Data).FirstOrDefault() ?? JpgBlobs.Select(p => p.Data).FirstOrDefault();
            if (image != null)
                target.ImageData = image;
        }

        // Restore the file drop whenever an HDROP entry exists, even for Text-type clips that were auto-expanded from HDROP.
        var filePathBlob = BinaryBlobs.FirstOrDefault(b => Formats.Any(p => p.Id == b.ClipDataId && p.Format == ClipboardFormats.HDrop.Code));
        if (filePathBlob != null)
            target.FilePathsJson = Encoding.UTF8.GetString(filePathBlob.Data);

        var dropEffectBlob = BinaryBlobs.FirstOrDefault(b => Formats.Any(p => p.Id == b.ClipDataId && p.FormatName == ClipboardFormats.PreferredDropEffect.Name));
        if (dropEffectBlob is { Data.Length: >= sizeof(int) })
            target.FileDropEffect = BitConverter.ToInt32(dropEffectBlob.Data);
    }
}
