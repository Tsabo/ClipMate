namespace ClipMate.Core.Constants;

/// <summary>
/// Represents a Windows clipboard format with its code and name.
/// </summary>
/// <param name="Code">The numeric format code used by Windows clipboard API.</param>
/// <param name="Name">The string identifier for the format.</param>
public record ClipboardFormat(int Code, string Name);

/// <summary>
/// Standard Windows Clipboard formats.
/// Each format contains both the Windows API code and string identifier.
/// See: https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats
/// </summary>
public static class Formats
{
    /// <summary>
    /// Text format (ANSI). Each line ends with CR-LF, null-terminated.
    /// Code: 1, Name: "TEXT"
    /// </summary>
    public static readonly ClipboardFormat Text = new(1, "TEXT");

    /// <summary>
    /// Bitmap format. Handle to a bitmap (HBITMAP).
    /// Code: 2, Name: "BITMAP"
    /// </summary>
    public static readonly ClipboardFormat Bitmap = new(2, "BITMAP");

    /// <summary>
    /// Windows metafile picture format.
    /// Code: 3, Name: "METAFILEPICT"
    /// </summary>
    public static readonly ClipboardFormat Metafilepict = new(3, "METAFILEPICT");

    /// <summary>
    /// Symbolic Link (SYLK) format.
    /// Code: 4, Name: "SYLK"
    /// </summary>
    public static readonly ClipboardFormat Sylk = new(4, "SYLK");

    /// <summary>
    /// Data Interchange Format (DIF).
    /// Code: 5, Name: "DIF"
    /// </summary>
    public static readonly ClipboardFormat Dif = new(5, "DIF");

    /// <summary>
    /// Tagged Image File Format (TIFF).
    /// Code: 6, Name: "TIFF"
    /// </summary>
    public static readonly ClipboardFormat Tiff = new(6, "TIFF");

    /// <summary>
    /// OEM text format.
    /// Code: 7, Name: "OEMTEXT"
    /// </summary>
    public static readonly ClipboardFormat OemText = new(7, "OEMTEXT");

    /// <summary>
    /// Device Independent Bitmap. Memory object with BITMAPINFO structure + bitmap bits.
    /// Code: 8, Name: "DIB"
    /// </summary>
    public static readonly ClipboardFormat Dib = new(8, "DIB");

    /// <summary>
    /// Color palette format.
    /// Code: 9, Name: "PALETTE"
    /// </summary>
    public static readonly ClipboardFormat Palette = new(9, "PALETTE");

    /// <summary>
    /// Pen data format for pen computing.
    /// Code: 10, Name: "PENDATA"
    /// </summary>
    public static readonly ClipboardFormat PenData = new(10, "PENDATA");

    /// <summary>
    /// Resource Interchange File Format (RIFF).
    /// Code: 11, Name: "RIFF"
    /// </summary>
    public static readonly ClipboardFormat Riff = new(11, "RIFF");

    /// <summary>
    /// Wave audio format.
    /// Code: 12, Name: "WAVE"
    /// </summary>
    public static readonly ClipboardFormat Wave = new(12, "WAVE");

    /// <summary>
    /// Unicode text format. Each line ends with CR-LF, null-terminated.
    /// Code: 13, Name: "CF_UNICODETEXT"
    /// </summary>
    public static readonly ClipboardFormat UnicodeText = new(13, "CF_UNICODETEXT");

    /// <summary>
    /// Enhanced metafile format.
    /// Code: 14, Name: "ENHMETAFILE"
    /// </summary>
    public static readonly ClipboardFormat EnhMetafile = new(14, "ENHMETAFILE");

    /// <summary>
    /// File drop list. Handle to HDROP identifying a list of files.
    /// Code: 15, Name: "HDROP"
    /// </summary>
    public static readonly ClipboardFormat HDrop = new(15, "HDROP");

    /// <summary>
    /// Locale information format.
    /// Code: 16, Name: "LOCALE"
    /// </summary>
    public static readonly ClipboardFormat Locale = new(16, "LOCALE");

    /// <summary>
    /// Rich Text Format (registered format).
    /// Code: 0x0082 (130), Name: "Rich Text Format"
    /// </summary>
    public static readonly ClipboardFormat RichText = new(0x0082, "Rich Text Format");

    /// <summary>
    /// HTML Format (registered format used by browsers).
    /// Code: 0x0080 (128), Name: "HTML Format"
    /// </summary>
    public static readonly ClipboardFormat Html = new(0x0080, "HTML Format");

    /// <summary>
    /// Alternative HTML Format code (varies by system).
    /// Code: 49161, Name: "HTML Format"
    /// </summary>
    public static readonly ClipboardFormat HtmlAlt = new(49161, "HTML Format");

    /// <summary>
    /// All standard formats as a collection for enumeration.
    /// </summary>
    public static readonly IReadOnlyList<ClipboardFormat> All =
    [
        Text, Bitmap, Metafilepict, Sylk, Dif, Tiff, OemText, Dib,
        Palette, PenData, Riff, Wave, UnicodeText, EnhMetafile, HDrop, Locale,
        RichText, Html, HtmlAlt
    ];
}

/// <summary>
/// ClipMate Storage Type values (maps to BLOB table selection).
/// These determine which BLOB table stores the actual clipboard data.
/// </summary>
public static class StorageType
{
    /// <summary>
    /// Text formats stored in BlobTxt table.
    /// Used for: CF_TEXT, CF_UNICODETEXT, CF_HTML, CF_RTF
    /// </summary>
    public const int Text = 1;

    /// <summary>
    /// JPEG images stored in BlobJpg table.
    /// </summary>
    public const int Jpeg = 2;

    /// <summary>
    /// PNG images stored in BlobPng table.
    /// </summary>
    public const int Png = 3;

    /// <summary>
    /// Other binary data stored in BlobBlob table.
    /// Used for: CF_BITMAP, CF_DIB, custom binary formats
    /// </summary>
    public const int Binary = 4;
}
