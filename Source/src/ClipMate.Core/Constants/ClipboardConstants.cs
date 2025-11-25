namespace ClipMate.Core.Constants;

/// <summary>
/// Standard Windows Clipboard format codes and ClipMate storage type constants.
/// </summary>
public static class ClipboardConstants
{
    /// <summary>
    /// Standard Windows Clipboard Format Codes.
    /// See: https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats
    /// </summary>
    public static class Format
    {
        /// <summary>
        /// Text format. Each line ends with a carriage return/linefeed (CR-LF) combination.
        /// A null character signals the end of the data.
        /// </summary>
        public const int CF_TEXT = 1;

        /// <summary>
        /// A handle to a bitmap (HBITMAP).
        /// </summary>
        public const int CF_BITMAP = 2;

        /// <summary>
        /// A memory object containing a BITMAPINFO structure followed by the bitmap bits.
        /// </summary>
        public const int CF_DIB = 8;

        /// <summary>
        /// Unicode text format. Each line ends with a carriage return/linefeed (CR-LF) combination.
        /// A null character signals the end of the data.
        /// </summary>
        public const int CF_UNICODETEXT = 13;

        /// <summary>
        /// Rich Text Format.
        /// Registered format code: 0x0082 (130 decimal)
        /// </summary>
        public const int CF_RTF = 0x0082;

        /// <summary>
        /// HTML Format (custom registered format used by browsers).
        /// Registered format code: 0x0080 (128 decimal) or 49161 depending on registration.
        /// Note: Different sources report different values. Use both for compatibility.
        /// </summary>
        public const int CF_HTML = 0x0080;

        /// <summary>
        /// Alternative HTML Format code (used in some contexts).
        /// </summary>
        public const int CF_HTML_ALT = 49161;

        /// <summary>
        /// A handle to type HDROP that identifies a list of files.
        /// An application can retrieve information about the files by passing the handle to DragQueryFile.
        /// </summary>
        public const int CF_HDROP = 15;
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
}
