using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using ClipMate.Core.Models;
using ClipMate.Data.Legacy.Dbisam;
using ClipMate.Data.Repositories;
using ClipMate.Platform.Helpers;

namespace ClipMate.Data.Legacy.ClipMate7;

/// <summary>
/// One stored format of a ClipMate 7 clip (a CLIPDATA row joined to its BLOB row).
/// </summary>
/// <param name="FormatName">CLIPDATA.FORMAT_NAME, e.g. "TEXT", "HTML Format", "Rich Text Format", "BITMAP".</param>
/// <param name="FormatCode">CLIPDATA.FORMAT. Standard clipboard formats use their Win32 codes; ClipMate 7
/// used negative codes for registered formats (-1 = RTF, -3 = HTML).</param>
/// <param name="Data">Raw BLOB bytes.</param>
internal sealed record ClipMate7Format(string FormatName, int FormatCode, byte[] Data);

/// <summary>
/// Converts ClipMate 7 clip formats into the transient content properties of a <see cref="Clip" />
/// (TextContent, RtfContent, HtmlContent, ImageData, FilePathsJson), which
/// <see cref="ClipContentStorage" /> then persists the same way as a freshly captured clip.
/// </summary>
internal static class ClipMate7FormatConverter
{
    private const string _htmlFormatName = "HTML Format";
    private const string _rtfFormatName = "Rich Text Format";

    private static readonly Encoding _strictUtf8 = new UTF8Encoding(false, true);

    /// <summary>
    /// Applies the formats to the clip, then sets <see cref="Clip.Type" />, <see cref="Clip.ContentHash" />
    /// and <see cref="Clip.Checksum" /> the same way clipboard capture does.
    /// </summary>
    /// <returns>Number of formats that could not be converted and were dropped.</returns>
    public static int Apply(Clip clip, IEnumerable<ClipMate7Format> formats)
    {
        var unsupported = 0;
        string? ansiText = null;

        foreach (var item in formats)
        {
            if (item.Data.Length == 0)
                continue;

            if (string.Equals(item.FormatName, _htmlFormatName, StringComparison.OrdinalIgnoreCase))
                clip.HtmlContent ??= DecodeUtf8OrAnsi(item.Data);
            else if (string.Equals(item.FormatName, _rtfFormatName, StringComparison.OrdinalIgnoreCase))
                clip.RtfContent ??= TrimNul(DbisamTable.Ansi.GetString(item.Data));
            else if (item.FormatCode == Formats.UnicodeText.Code)
                clip.TextContent ??= TrimNul(Encoding.Unicode.GetString(item.Data));
            else if (item.FormatCode == Formats.Text.Code || item.FormatCode == Formats.OemText.Code)
                ansiText ??= TrimNul(DbisamTable.Ansi.GetString(item.Data));
            else if (IsImage(item))
                clip.ImageData ??= item.Data;
            else if (item.FormatCode == Formats.HDrop.Code && TryParseDropFiles(item.Data, out var filePaths))
                clip.FilePathsJson ??= JsonSerializer.Serialize(filePaths);
            else
                unsupported++;
        }

        // Prefer CF_UNICODETEXT over CF_TEXT when both exist
        clip.TextContent ??= ansiText;

        if (clip.FilePathsJson is not null && clip.TextContent is null)
        {
            var filePaths = JsonSerializer.Deserialize<List<string>>(clip.FilePathsJson) ?? [];
            clip.TextContent = string.Join(Environment.NewLine, filePaths);
        }

        clip.Type = DetermineType(clip);
        clip.ContentHash = clip.Type switch
        {
            ClipType.Image => ContentHasher.HashBytes(clip.ImageData),
            ClipType.Files => ContentHasher.HashText(clip.FilePathsJson ?? string.Empty),
            var _ => ContentHasher.HashText(clip.TextContent ?? string.Empty),
        };

        clip.Checksum = clip.ContentHash.GetHashCode();

        return unsupported;
    }

    /// <summary>
    /// True if the clip ended up with any content that can be stored.
    /// </summary>
    public static bool HasContent(Clip clip) =>
        !string.IsNullOrEmpty(clip.TextContent) ||
        !string.IsNullOrEmpty(clip.RtfContent) ||
        !string.IsNullOrEmpty(clip.HtmlContent) ||
        clip.ImageData is { Length: > 0 } ||
        !string.IsNullOrEmpty(clip.FilePathsJson);

    private static ClipType DetermineType(Clip clip)
    {
        if (clip.ImageData is { Length: > 0 } && string.IsNullOrEmpty(clip.TextContent))
            return ClipType.Image;

        if (clip.FilePathsJson is not null)
            return ClipType.Files;

        if (!string.IsNullOrEmpty(clip.HtmlContent))
            return ClipType.Html;

        if (!string.IsNullOrEmpty(clip.RtfContent))
            return ClipType.RichText;

        return clip.ImageData is { Length: > 0 }
            ? ClipType.Image
            : ClipType.Text;
    }

    /// <summary>
    /// ClipMate 7 stores bitmaps as PNG (BLOBPNG) or JPEG (BLOBJPG). Raw DIBs are not converted.
    /// </summary>
    private static bool IsImage(ClipMate7Format format) =>
        ClipContentStorage.DetectImageFormat(format.Data) is ClipContentStorage.ImageFormat.Png or ClipContentStorage.ImageFormat.Jpeg;

    /// <summary>
    /// CF_HTML is UTF-8 by specification; fall back to Windows-1252 for malformed data.
    /// </summary>
    private static string DecodeUtf8OrAnsi(byte[] data)
    {
        try
        {
            return TrimNul(_strictUtf8.GetString(data));
        }
        catch (DecoderFallbackException)
        {
            return TrimNul(DbisamTable.Ansi.GetString(data));
        }
    }

    private static string TrimNul(string value) => value.TrimEnd('\0');

    /// <summary>
    /// Parses a CF_HDROP payload: a DROPFILES header (pFiles offset, POINT, fNC, fWide) followed by a
    /// double-NUL-terminated list of paths.
    /// </summary>
    internal static bool TryParseDropFiles(byte[] data, out List<string> filePaths)
    {
        filePaths = [];
        if (data.Length < 20)
            return false;

        var offset = BinaryPrimitives.ReadInt32LittleEndian(data);
        var isWide = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(16)) != 0;
        if (offset < 20 || offset >= data.Length)
            return false;

        var text = isWide
            ? Encoding.Unicode.GetString(data, offset, (data.Length - offset) & ~1)
            : DbisamTable.Ansi.GetString(data, offset, data.Length - offset);

        foreach (var item in text.Split('\0'))
        {
            if (item.Length == 0)
                break;

            filePaths.Add(item);
        }

        return filePaths.Count > 0;
    }
}
