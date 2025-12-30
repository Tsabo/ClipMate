using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Export;

namespace ClipMate.Core.Services;

/// <summary>
/// Service interface for exporting and importing clips in flat-file and XML formats.
/// </summary>
public interface IExportImportService
{
    /// <summary>
    /// Exports clips to individual files (TXT for text, BMP/JPEG/PNG for images).
    /// </summary>
    /// <param name="clips">Clips to export.</param>
    /// <param name="exportDirectory">Directory to export files to.</param>
    /// <param name="namingStrategy">File naming strategy to use.</param>
    /// <param name="resetSequence">If true and strategy is Sequential, start sequence at startSequence value.</param>
    /// <param name="jpegQuality">JPEG compression quality (0-100).</param>
    /// <param name="progressCallback">Optional callback for progress updates.</param>
    /// <param name="startSequence">Starting sequence number (default 1).</param>
    /// <param name="imageFormat">Image export format (PNG, JPG, BMP). Defaults to PNG.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next sequence number to use for future exports.</returns>
    Task<int> ExportClipsToFilesAsync(IEnumerable<Clip> clips,
        string exportDirectory,
        FileNamingStrategy namingStrategy,
        bool resetSequence = false,
        int jpegQuality = 85,
        Action<ExportProgressMessage>? progressCallback = null,
        int startSequence = 1,
        ImageExportFormat imageFormat = ImageExportFormat.Png,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports clips and collections to XML format.
    /// </summary>
    /// <param name="clips">Clips to export.</param>
    /// <param name="collections">Collections to export.</param>
    /// <param name="xmlFilePath">Path to XML file to create.</param>
    /// <param name="progressCallback">Optional callback for progress updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExportToXmlAsync(IEnumerable<Clip> clips,
        IEnumerable<Collection> collections,
        string xmlFilePath,
        Action<ExportProgressMessage>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports clips and collections from XML file.
    /// </summary>
    /// <param name="xmlFilePath">Path to XML file to import.</param>
    /// <param name="targetCollectionId">Optional target collection ID for imported clips.</param>
    /// <param name="progressCallback">Optional callback for progress updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import data with clips and collections ready for insertion.</returns>
    Task<XmlImportData> ImportFromXmlAsync(string xmlFilePath,
        Guid? targetCollectionId = null,
        Action<ExportProgressMessage>? progressCallback = null,
        CancellationToken cancellationToken = default);
}
