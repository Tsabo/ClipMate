namespace ClipMate.Core.Constants;

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
