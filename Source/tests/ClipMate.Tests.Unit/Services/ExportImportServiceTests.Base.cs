using System.Text;
using System.Text.RegularExpressions;
using ClipMate.Core.Models;
using ClipMate.Platform.Services;

namespace ClipMate.Tests.Unit.Services;

public abstract partial class ExportImportServiceTestsBase : TestFixtureBase
{
    protected ExportImportService CreateService()
    {
        var logger = CreateLogger<ExportImportService>();
        return new ExportImportService(logger);
    }

    protected static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ClipMateTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    protected static string CreateTempFilePath(string extension)
    {
        var dir = CreateTempDirectory();
        return Path.Combine(dir, $"test-{Guid.NewGuid():N}{extension}");
    }

    protected static List<Clip> CreateClips(params string[] titles)
    {
        return titles.Select(p => new Clip
            {
                Id = Guid.NewGuid(),
                Title = p,
                CapturedAt = DateTimeOffset.UtcNow,
                ContentHash = Guid.NewGuid().ToString("N"),
                Type = ClipType.Text,
            })
            .ToList();
    }

    protected static void PreCreateSequentialFiles(string directory, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            var path = Path.Combine(directory, $"{i:00000}.txt");
            File.WriteAllText(path, $"pre-{i}", Encoding.UTF8);
        }
    }

    /// <summary>
    /// Creates a clip with image data (a simple 2x2 red PNG).
    /// </summary>
    protected static Clip CreateImageClip(string? title = "Image Clip")
    {
        // Minimal valid 2x2 red PNG (67 bytes)
        var pngData = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, // 2x2 dimensions
            0x08, 0x02, 0x00, 0x00, 0x00, 0xFD, 0xD4, 0x9A, 0x73, // RGB, CRC
            0x00, 0x00, 0x00, 0x14, 0x49, 0x44, 0x41, 0x54, // IDAT chunk
            0x78, 0x9C, 0x62, 0xF8, 0xCF, 0xC0, 0x00, 0x00, // Compressed data
            0x00, 0x06, 0x00, 0x02, 0x54, 0xEF, 0x8C, 0x82, 0x00, 0x00, // Continued
            0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82, // IEND chunk
        };

        return new Clip
        {
            Id = Guid.NewGuid(),
            Title = title,
            CapturedAt = DateTimeOffset.UtcNow,
            ContentHash = Guid.NewGuid().ToString("N"),
            Type = ClipType.Image,
            ImageData = pngData,
        };
    }

    /// <summary>
    /// Creates a clip with HTML content.
    /// </summary>
    protected static Clip CreateHtmlClip(string html, string? title = "HTML Clip") =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            CapturedAt = DateTimeOffset.UtcNow,
            ContentHash = Guid.NewGuid().ToString("N"),
            Type = ClipType.Html,
            HtmlContent = html,
            TextContent = StripHtmlTags(html),
        };

    /// <summary>
    /// Creates a clip with RichText (RTF) content.
    /// </summary>
    protected static Clip CreateRtfClip(string rtf, string? title = "RTF Clip") =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            CapturedAt = DateTimeOffset.UtcNow,
            ContentHash = Guid.NewGuid().ToString("N"),
            Type = ClipType.RichText,
            RtfContent = rtf,
            TextContent = "Plain text fallback",
        };

    /// <summary>
    /// Creates a clip with file list content.
    /// </summary>
    protected static Clip CreateFilesClip(string[] filePaths, string? title = "Files Clip") =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            CapturedAt = DateTimeOffset.UtcNow,
            ContentHash = Guid.NewGuid().ToString("N"),
            Type = ClipType.Files,
            FilePathsJson = string.Join(Environment.NewLine, filePaths),
        };

    // Simple tag removal for test purposes
    private static string StripHtmlTags(string html) => StripHtmlTagsRegEx().Replace(html, string.Empty);

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex StripHtmlTagsRegEx();
}
