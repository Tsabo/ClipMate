using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Export;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for exporting and importing clips in flat-file and XML formats.
/// Handles all export/import operations with support for sequential naming, title-based naming, GUID-based naming, and
/// per-file prompts.
/// </summary>
public class ExportImportService : IExportImportService
{
    private readonly ILogger<ExportImportService> _logger;

    public ExportImportService(ILogger<ExportImportService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Exports clips to individual files (TXT for text, BMP/JPEG/PNG for images).
    /// </summary>
    /// <returns>The next sequence number to use for future exports.</returns>
    public async Task<int> ExportClipsToFilesAsync(IEnumerable<Clip> clips,
        string exportDirectory,
        FileNamingStrategy namingStrategy,
        bool resetSequence = false,
        int jpegQuality = 85,
        Action<ExportProgressMessage>? progressCallback = null,
        int startSequence = 1,
        ImageExportFormat imageFormat = ImageExportFormat.Png,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure export directory exists
            Directory.CreateDirectory(exportDirectory);

            var clipList = clips.ToList();
            if (clipList.Count == 0)
            {
                progressCallback?.Invoke(new ExportProgressMessage
                {
                    IsComplete = true,
                    Message = "No clips to export.",
                    Successful = true,
                });

                return startSequence;
            }

            // Initialize sequence - use startSequence if resetSequence, otherwise find next available
            var sequence = resetSequence
                ? startSequence
                : GetNextSequenceNumber(exportDirectory, startSequence);

            var exportedFiles = new List<string>();
            var processed = 0;

            foreach (var item in clipList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileName = namingStrategy switch
                    {
                        FileNamingStrategy.Sequential => $"{sequence:00000}",
                        FileNamingStrategy.TitleBased => SanitizeFileName(item.Title ?? "Untitled"),
                        FileNamingStrategy.Serial => item.Id.ToString("N"),
                        FileNamingStrategy.PromptPerFile => throw new NotSupportedException("PromptPerFile requires UI interaction"),
                        var _ => throw new InvalidOperationException($"Unknown naming strategy: {namingStrategy}"),
                    };

                    // Determine file extension based on clip content
                    var (content, extension) = await ExtractClipContent(item, imageFormat, jpegQuality, cancellationToken);

                    if (content != null)
                    {
                        var fullPath = Path.Combine(exportDirectory, $"{fileName}{extension}");

                        // Handle filename conflicts for sequential numbering
                        if (File.Exists(fullPath) && namingStrategy == FileNamingStrategy.Sequential)
                        {
                            sequence++;
                            fullPath = Path.Combine(exportDirectory, $"{sequence:00000}{extension}");
                        }

                        // Write file
                        if (content is byte[] bytes)
                            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
                        else if (content is string text)
                            await File.WriteAllTextAsync(fullPath, text, Encoding.UTF8, cancellationToken);

                        exportedFiles.Add(fullPath);
                        sequence++;

                        progressCallback?.Invoke(new ExportProgressMessage
                        {
                            IsComplete = false,
                            Message = $"Exported: {Path.GetFileName(fullPath)}",
                            Successful = true,
                            ProcessedCount = ++processed,
                            TotalCount = clipList.Count,
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to export clip {ClipId}", item.Id);
                    progressCallback?.Invoke(new ExportProgressMessage
                    {
                        IsComplete = false,
                        Message = $"Failed to export clip: {ex.Message}",
                        Successful = false,
                        ProcessedCount = ++processed,
                        TotalCount = clipList.Count,
                    });
                }
            }

            progressCallback?.Invoke(new ExportProgressMessage
            {
                IsComplete = true,
                Message = $"Export complete. {exportedFiles.Count} file(s) exported to: {exportDirectory}",
                Successful = true,
                ProcessedCount = processed,
                TotalCount = clipList.Count,
            });

            _logger.LogInformation("Successfully exported {Count} clips to {Directory}", exportedFiles.Count, exportDirectory);

            return sequence;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Export operation cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export operation failed");
            progressCallback?.Invoke(new ExportProgressMessage
            {
                IsComplete = true,
                Message = $"Export failed: {ex.Message}",
                Successful = false,
            });

            throw;
        }
    }

    /// <summary>
    /// Exports clips and collections to XML format.
    /// </summary>
    public async Task ExportToXmlAsync(IEnumerable<Clip> clips,
        IEnumerable<Collection> collections,
        string xmlFilePath,
        Action<ExportProgressMessage>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clipList = clips.ToList();
            var collectionList = collections.ToList();

            progressCallback?.Invoke(new ExportProgressMessage
            {
                IsComplete = false,
                Message = "Preparing XML export...",
                Successful = true,
            });

            var exportData = new XmlExportData
            {
                ExportedAt = DateTime.UtcNow,
                Version = "1.0",
                ClipCount = clipList.Count,
                CollectionCount = collectionList.Count,
                Clips = clipList.Select(ClipExportDto.FromClip).ToList(),
                Collections = collectionList.Select(CollectionExportDto.FromCollection).ToList(),
            };

            // Serialize to XML asynchronously
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var serializer = new XmlSerializer(typeof(XmlExportData));
                using var fileStream = new FileStream(xmlFilePath, FileMode.Create, FileAccess.Write);
                using var writer = new StreamWriter(fileStream, Encoding.UTF8);
                serializer.Serialize(writer, exportData);
            }, cancellationToken);

            progressCallback?.Invoke(new ExportProgressMessage
            {
                IsComplete = true,
                Message = $"XML export complete: {clipList.Count} clip(s), {collectionList.Count} collection(s)",
                Successful = true,
                ProcessedCount = clipList.Count,
                TotalCount = clipList.Count,
            });

            _logger.LogInformation("Successfully exported {ClipCount} clips to {FilePath}", clipList.Count, xmlFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XML export failed");
            progressCallback?.Invoke(new ExportProgressMessage
            {
                IsComplete = true,
                Message = $"XML export failed: {ex.Message}",
                Successful = false,
            });

            throw;
        }
    }

    /// <summary>
    /// Imports clips and collections from XML file.
    /// </summary>
    public async Task<XmlImportData> ImportFromXmlAsync(string xmlFilePath,
        Guid? targetCollectionId = null,
        Action<ExportProgressMessage>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback?.Invoke(new ExportProgressMessage
            {
                IsComplete = false,
                Message = "Reading XML file...",
                Successful = true,
            });

            if (!File.Exists(xmlFilePath))
                throw new FileNotFoundException($"XML file not found: {xmlFilePath}");

            var exportData = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var serializer = new XmlSerializer(typeof(XmlExportData));
                using var fileStream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(fileStream, Encoding.UTF8);
                return serializer.Deserialize(reader) as XmlExportData;
            }, cancellationToken);

            if (exportData == null)
                throw new InvalidOperationException("Failed to deserialize XML file");

            progressCallback?.Invoke(new ExportProgressMessage
            {
                IsComplete = false,
                Message = $"Preparing to import {exportData.ClipCount} clip(s)...",
                Successful = true,
            });

            var importData = new XmlImportData
            {
                ImportedAt = DateTime.UtcNow,
                SourceFile = xmlFilePath,
                TargetCollectionId = targetCollectionId ?? Guid.Empty,
                Clips = exportData.Clips.Select(p => p.ToClip()).ToList(),
                Collections = exportData.Collections.Select(p => p.ToCollection()).ToList(),
                ClipCount = exportData.ClipCount,
                CollectionCount = exportData.CollectionCount,
            };

            progressCallback?.Invoke(new ExportProgressMessage
            {
                IsComplete = true,
                Message = $"XML import ready: {importData.ClipCount} clip(s), {importData.CollectionCount} collection(s)",
                Successful = true,
            });

            return importData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XML import preparation failed");
            progressCallback?.Invoke(new ExportProgressMessage
            {
                IsComplete = true,
                Message = $"XML import failed: {ex.Message}",
                Successful = false,
            });

            throw;
        }
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Extracts content from a clip based on its type and available data.
    /// Returns the content and appropriate file extension.
    /// </summary>
    private static Task<(object? Content, string Extension)> ExtractClipContent(Clip clip,
        ImageExportFormat imageFormat,
        int jpegQuality,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken; // Parameter reserved for future use

        return Task.FromResult(clip.Type switch
        {
            ClipType.Image => ExtractImageContent(clip, imageFormat, jpegQuality),
            ClipType.Html => ExtractHtmlContent(clip),
            ClipType.RichText => ExtractRichTextContent(clip),
            ClipType.Text => ExtractTextContent(clip),
            ClipType.Files => ExtractFilesContent(clip),
            var _ => ExtractFallbackContent(clip, imageFormat, jpegQuality),
        });
    }

    /// <summary>
    /// Extracts image content from a clip, converting to the specified format.
    /// </summary>
    private static (object? Content, string Extension) ExtractImageContent(Clip clip,
        ImageExportFormat imageFormat,
        int jpegQuality)
    {
        // Return fallback if no image data available
        if (clip.ImageData is not { Length: > 0 })
            return ExtractFallbackContent(clip, imageFormat, jpegQuality);

        // Convert image to the requested format
        var (convertedData, extension) = ConvertImageToFormat(clip.ImageData, imageFormat, jpegQuality);
        return (convertedData, extension);
    }

    /// <summary>
    /// Converts image data to the specified export format.
    /// </summary>
    private static (byte[] Data, string Extension) ConvertImageToFormat(byte[] imageData,
        ImageExportFormat format,
        int jpegQuality)
    {
        try
        {
            using var inputStream = new MemoryStream(imageData);
            var decoder = BitmapDecoder.Create(inputStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            using var outputStream = new MemoryStream();
            BitmapEncoder encoder = format switch
            {
                ImageExportFormat.Jpg => new JpegBitmapEncoder { QualityLevel = jpegQuality },
                ImageExportFormat.Bmp => new BmpBitmapEncoder(),
                var _ => new PngBitmapEncoder(), // Default to PNG
            };

            encoder.Frames.Add(BitmapFrame.Create(frame));
            encoder.Save(outputStream);

            var extension = format switch
            {
                ImageExportFormat.Jpg => ".jpg",
                ImageExportFormat.Bmp => ".bmp",
                var _ => ".png",
            };

            return (outputStream.ToArray(), extension);
        }
        catch
        {
            // If conversion fails, return original data with best-guess extension
            return (imageData, ".png");
        }
    }

    /// <summary>
    /// Extracts HTML content from a clip.
    /// </summary>
    private static (object? Content, string Extension) ExtractHtmlContent(Clip clip)
    {
        // Prefer HTML content for HTML clips
        if (!string.IsNullOrEmpty(clip.HtmlContent))
            return (clip.HtmlContent, ".html");

        // Fall back to plain text if HTML not available
        if (!string.IsNullOrEmpty(clip.TextContent))
            return (clip.TextContent, ".txt");

        return ExtractFallbackContent(clip);
    }

    /// <summary>
    /// Extracts rich text content from a clip.
    /// </summary>
    private static (object? Content, string Extension) ExtractRichTextContent(Clip clip)
    {
        // Prefer RTF content for RichText clips
        if (!string.IsNullOrEmpty(clip.RtfContent))
            return (clip.RtfContent, ".rtf");

        // Fall back to plain text if RTF not available
        if (!string.IsNullOrEmpty(clip.TextContent))
            return (clip.TextContent, ".txt");

        return ExtractFallbackContent(clip);
    }

    /// <summary>
    /// Extracts text content from a clip.
    /// </summary>
    private static (object? Content, string Extension) ExtractTextContent(Clip clip)
    {
        if (!string.IsNullOrEmpty(clip.TextContent))
            return (clip.TextContent, ".txt");

        return ExtractFallbackContent(clip);
    }

    /// <summary>
    /// Extracts file list content from a clip.
    /// </summary>
    private static (object? Content, string Extension) ExtractFilesContent(Clip clip)
    {
        // Export file paths as a text list
        if (!string.IsNullOrEmpty(clip.FilePathsJson))
            return (clip.FilePathsJson, ".json");

        // Fall back to text content if available
        if (!string.IsNullOrEmpty(clip.TextContent))
            return (clip.TextContent, ".txt");

        return ExtractFallbackContent(clip);
    }

    /// <summary>
    /// Fallback content extraction when specific format is not available.
    /// </summary>
    private static (object? Content, string Extension) ExtractFallbackContent(Clip clip,
        ImageExportFormat imageFormat = ImageExportFormat.Png,
        int jpegQuality = 85)
    {
        // Try text content first
        if (!string.IsNullOrEmpty(clip.TextContent))
            return (clip.TextContent, ".txt");

        // Try HTML content
        if (!string.IsNullOrEmpty(clip.HtmlContent))
            return (clip.HtmlContent, ".html");

        // Try RTF content
        if (!string.IsNullOrEmpty(clip.RtfContent))
            return (clip.RtfContent, ".rtf");

        // Try image data - convert to requested format
        if (clip.ImageData is { Length: > 0 })
        {
            var (convertedData, extension) = ConvertImageToFormat(clip.ImageData, imageFormat, jpegQuality);
            return (convertedData, extension);
        }

        // Last resort: export title as text
        var title = clip.Title ?? "Untitled Clip";
        return (title, ".txt");
    }

    /// <summary>
    /// Sanitizes a filename by removing or replacing invalid characters.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        // Remove invalid file name characters
        var invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var regex = new Regex($"[{Regex.Escape(invalidChars)}]");
        var sanitized = regex.Replace(fileName, "_");

        // Limit length to 255 characters (filesystem limit)
        if (sanitized.Length > 255)
            sanitized = sanitized[..255];

        return string.IsNullOrWhiteSpace(sanitized)
            ? "Untitled"
            : sanitized;
    }

    /// <summary>
    /// Determines the next sequence number by scanning existing files in the export directory.
    /// Uses the higher of the startSequence or the highest existing file number + 1.
    /// </summary>
    private static int GetNextSequenceNumber(string exportDirectory, int startSequence = 1)
    {
        if (!Directory.Exists(exportDirectory))
            return startSequence;

        var files = Directory.GetFiles(exportDirectory);
        if (files.Length == 0)
            return startSequence;

        // Find the highest numeric prefix
        var maxSequence = 0;
        foreach (var item in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(item);
            var numericPart = new string(fileName.Take(5).ToArray());
            if (int.TryParse(numericPart, out var sequence))
                maxSequence = Math.Max(maxSequence, sequence);
        }

        // Return the higher of stored sequence or file-based sequence
        return Math.Max(startSequence, maxSequence + 1);
    }
}
