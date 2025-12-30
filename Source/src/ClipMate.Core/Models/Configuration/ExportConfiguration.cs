using ClipMate.Core.Models.Export;

namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Configuration for clip export/import operations.
/// </summary>
public class ExportConfiguration
{
    /// <summary>
    /// Gets or sets whether to copy exported file names to clipboard.
    /// </summary>
    public bool CopyNamesToClipboard { get; set; }

    /// <summary>
    /// Gets or sets the date of the last export operation.
    /// </summary>
    public DateTime? DateLastExport { get; set; }

    /// <summary>
    /// Gets or sets the date of the last import operation.
    /// </summary>
    public DateTime? DateLastImport { get; set; }

    /// <summary>
    /// Gets or sets whether to erase directory contents before exporting.
    /// </summary>
    public bool EraseDirectoryContents { get; set; }

    /// <summary>
    /// Gets or sets the default export directory for flat-file exports.
    /// If null/empty, defaults to Documents\ClipMateExport at runtime.
    /// </summary>
    public string? ExportDirectory { get; set; }

    /// <summary>
    /// Gets or sets the image format for exporting images (0=JPG, 1=PNG, 2=BMP).
    /// </summary>
    public ImageExportFormat ImageFormat { get; set; } = ImageExportFormat.Png;

    /// <summary>
    /// Gets or sets the file naming convention.
    /// </summary>
    public FileNamingStrategy FileNamingStrategy { get; set; } = FileNamingStrategy.Sequential;

    /// <summary>
    /// Gets or sets the JPEG quality (0-100).
    /// </summary>
    public int JpegQuality { get; set; } = 85;

    /// <summary>
    /// Gets or sets the default import directory.
    /// </summary>
    public string? ImportDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether to open the folder when export is finished.
    /// </summary>
    public bool OpenFolderWhenFinished { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to reset the sequence number before exporting.
    /// </summary>
    public bool ResetSequence { get; set; }

    /// <summary>
    /// Gets or sets the current sequence number for sequential naming.
    /// </summary>
    public int Sequence { get; set; } = 1;

    /// <summary>
    /// Gets the resolved export directory path, with environment variables expanded.
    /// Returns Documents\ClipMateExport if not configured.
    /// </summary>
    public string GetResolvedExportDirectory()
    {
        if (!string.IsNullOrEmpty(ExportDirectory))
            return Environment.ExpandEnvironmentVariables(ExportDirectory);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ClipMateExport");
    }

    /// <summary>
    /// Gets the resolved import directory path, with environment variables expanded.
    /// Falls back to export directory if not configured.
    /// </summary>
    public string GetResolvedImportDirectory() => !string.IsNullOrEmpty(ImportDirectory)
        ? Environment.ExpandEnvironmentVariables(ImportDirectory)
        : GetResolvedExportDirectory();
}

/// <summary>
/// Image export format options.
/// </summary>
public enum ImageExportFormat
{
    /// <summary>JPEG format.</summary>
    Jpg = 0,

    /// <summary>PNG format.</summary>
    Png = 1,

    /// <summary>BMP format.</summary>
    Bmp = 2,
}
