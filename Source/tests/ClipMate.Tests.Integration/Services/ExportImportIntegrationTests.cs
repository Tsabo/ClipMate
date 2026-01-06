using System.Xml.Serialization;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Export;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for XML export/import with real database persistence.
/// Verifies that clips saved to database can be exported with all blob data intact.
/// </summary>
public class ExportImportIntegrationTests : IntegrationTestBase
{
    private const string _testDatabaseKey = "db_test0001";

    [Test]
    public async Task ExportToXml_WithTextClipFromDatabase_IncludesTextContent()
    {
        // Arrange - Save clip to database with text content
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Hello World from Database",
            Title = "Test Text Clip",
            ContentHash = "test_hash_text",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Verify the clip was saved
        await Assert.That(savedClip.Id).IsNotEqualTo(Guid.Empty);

        // Load clip back from database using a new context (simulates real scenario)
        var connection = DbContext.Database.GetDbConnection();
        var newContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);

        var newClipService = CreateClipServiceWithContext(newContext);

        var loadedClip = await newClipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(loadedClip).IsNotNull();

        // Verify blob data is NOT loaded initially (this is the real-world scenario)
        await Assert.That(loadedClip?.TextContent).IsNull();

        // Load blob data before export (the fix we implemented)
        await clipService.LoadBlobDataAsync(_testDatabaseKey, loadedClip);

        // Act - Export with blob data loaded
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync([loadedClip], [], xmlPath);

        // Assert - Verify XML contains text content
        var xmlContent = await File.ReadAllTextAsync(xmlPath);
        await Assert.That(xmlContent).Contains("<TextContent>Hello World from Database</TextContent>");

        // Also verify via deserialization
        var serializer = new XmlSerializer(typeof(XmlExportData));
        await using var fs = File.OpenRead(xmlPath);
        var exportData = (XmlExportData?)serializer.Deserialize(fs);
        await Assert.That(exportData).IsNotNull();
        await Assert.That(exportData!.Clips).Count().IsEqualTo(1);
        await Assert.That(exportData.Clips[0].TextContent).IsEqualTo("Hello World from Database");

        // Verify re-import works
        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        await Assert.That(importData.Clips[0].TextContent).IsEqualTo("Hello World from Database");

        // Cleanup
        await using (newContext) { }
    }

    [Test]
    public async Task ExportToXml_WithHtmlClipFromDatabase_IncludesHtmlContent()
    {
        // Arrange - Save HTML clip to database
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Html,
            HtmlContent = "<p>HTML content from database</p>",
            TextContent = "HTML content from database",
            Title = "Test HTML Clip",
            ContentHash = "test_hash_html",
            CapturedAt = DateTime.UtcNow,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Load clip back and load blob data
        var loadedClip = await clipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(loadedClip).IsNotNull();
        await clipService.LoadBlobDataAsync(_testDatabaseKey, loadedClip!);

        // Act - Export
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync([loadedClip!], [], xmlPath);

        // Assert - Verify XML contains HTML content
        var xmlContent = await File.ReadAllTextAsync(xmlPath);
        await Assert.That(xmlContent).Contains("<HtmlContent>&lt;p&gt;HTML content from database&lt;/p&gt;</HtmlContent>");

        // Verify re-import
        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        await Assert.That(importData.Clips[0].HtmlContent).IsEqualTo("<p>HTML content from database</p>");
    }

    [Test]
    public async Task ExportToXml_WithImageClipFromDatabase_IncludesImageData()
    {
        // Arrange - Save image clip to database
        var clipService = CreateClipService();
        var imageData = CreateTestImageData();
        var clip = new Clip
        {
            Type = ClipType.Image,
            ImageData = imageData,
            Title = "Test Image Clip",
            ContentHash = "test_hash_image",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Load clip back and load blob data
        var loadedClip = await clipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(loadedClip).IsNotNull();

        // Load blob data (in real scenario this might not be needed if data persisted with clip)
        await clipService.LoadBlobDataAsync(_testDatabaseKey, loadedClip!);

        // Assert blob data is loaded
        await Assert.That(loadedClip?.ImageData!).IsEquivalentTo(imageData);

        // Act - Export
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync([loadedClip!], [], xmlPath);

        // Assert - Verify XML contains base64 image data
        var xmlContent = await File.ReadAllTextAsync(xmlPath);
        await Assert.That(xmlContent).Contains("<ImageDataBase64>");

        // Verify re-import restores image data exactly
        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        await Assert.That(importData.Clips[0].ImageData!).IsNotNull();
        await Assert.That(importData.Clips[0].ImageData!).IsEquivalentTo(imageData);
    }

    [Test]
    public async Task ExportToXml_WithFilesClipFromDatabase_IncludesFilePathsJson()
    {
        // Arrange - Save files clip to database
        var clipService = CreateClipService();
        const string filePaths = "C:\\test\\file1.txt\r\nC:\\test\\file2.pdf";
        var clip = new Clip
        {
            Type = ClipType.Files,
            FilePathsJson = filePaths,
            Title = "Test Files Clip",
            ContentHash = "test_hash_files",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Load clip back and load blob data
        var loadedClip = await clipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(loadedClip).IsNotNull();

        // Load blob data (in real scenario this ensures all blob data is present)
        await clipService.LoadBlobDataAsync(_testDatabaseKey, loadedClip!);

        // Act - Export
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync([loadedClip!], [], xmlPath);

        // Assert - Verify XML contains file paths
        var xmlContent = await File.ReadAllTextAsync(xmlPath);
        await Assert.That(xmlContent).Contains("<FilePathsJson>");

        // Verify re-import
        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        await Assert.That(importData.Clips[0].FilePathsJson?.Replace("\r\n", "\n")).IsEqualTo(filePaths.Replace("\r\n", "\n"));
    }

    [Test]
    public async Task ExportToXml_WithMultipleClipsFromDatabase_PreservesAllBlobData()
    {
        // Arrange - Save multiple clips with different types
        var clipService = CreateClipService();
        var clips = new List<Clip>
        {
            new()
            {
                Type = ClipType.Text,
                TextContent = "Text clip content",
                Title = "Text Clip",
                ContentHash = "hash_1",
                CapturedAt = DateTime.UtcNow,
                CollectionId = Guid.Empty,
            },
            new()
            {
                Type = ClipType.Html,
                HtmlContent = "<b>HTML</b>",
                TextContent = "HTML",
                Title = "HTML Clip",
                ContentHash = "hash_2",
                CapturedAt = DateTime.UtcNow,
                CollectionId = Guid.Empty,
            },
            new()
            {
                Type = ClipType.Image,
                ImageData = CreateTestImageData(),
                Title = "Image Clip",
                ContentHash = "hash_3",
                CapturedAt = DateTime.UtcNow,
                CollectionId = Guid.Empty,
            },
        };

        foreach (var clip in clips)
            await clipService.CreateAsync(_testDatabaseKey, clip);

        await DbContext.SaveChangesAsync();

        // Load clips back
        var loadedClips = await clipService.GetByCollectionAsync(_testDatabaseKey, Guid.Empty);

        // Load blob data for all clips
        foreach (var clip in loadedClips)
            await clipService.LoadBlobDataAsync(_testDatabaseKey, clip);

        // Act - Export all clips
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync(loadedClips, [], xmlPath);

        // Assert - Verify all clips have their content
        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        await Assert.That(importData.Clips.Count).IsEqualTo(3);

        var textClip = importData.Clips.First(c => c.Type == ClipType.Text);
        await Assert.That(textClip.TextContent).IsEqualTo("Text clip content");

        var htmlClip = importData.Clips.First(c => c.Type == ClipType.Html);
        await Assert.That(htmlClip.HtmlContent).IsEqualTo("<b>HTML</b>");

        var imageClip = importData.Clips.First(c => c.Type == ClipType.Image);
        await Assert.That(imageClip.ImageData!).IsNotNull();
    }

    #region Helper Methods

    private IClipService CreateClipService()
    {
        var contextFactory = new Mock<IDatabaseContextFactory>();

        // Setup factory to return real repositories for the test database
        var clipLogger = Mock.Of<ILogger<ClipRepository>>();
        var clipRepository = new ClipRepository(DbContext, clipLogger);
        contextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey))
            .Returns(clipRepository);

        var clipDataRepository = new ClipDataRepository(DbContext);
        contextFactory.Setup(p => p.GetClipDataRepository(_testDatabaseKey))
            .Returns(clipDataRepository);

        var blobRepository = new BlobRepository(DbContext);
        contextFactory.Setup(p => p.GetBlobRepository(_testDatabaseKey))
            .Returns(blobRepository);

        var serviceLogger = Mock.Of<ILogger<ClipService>>();

        return new ClipService(
            contextFactory.Object,
            Mock.Of<IConfigurationService>(),
            Mock.Of<IClipboardService>(),
            Mock.Of<ITemplateService>(),
            serviceLogger);
    }

    private IClipService CreateClipServiceWithContext(ClipMateDbContext context)
    {
        var contextFactory = new Mock<IDatabaseContextFactory>();

        // Setup factory to return real repositories with the provided context
        var clipLogger = Mock.Of<ILogger<ClipRepository>>();
        var clipRepository = new ClipRepository(context, clipLogger);
        contextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey))
            .Returns(clipRepository);

        var clipDataRepository = new ClipDataRepository(context);
        contextFactory.Setup(p => p.GetClipDataRepository(_testDatabaseKey))
            .Returns(clipDataRepository);

        var blobRepository = new BlobRepository(context);
        contextFactory.Setup(p => p.GetBlobRepository(_testDatabaseKey))
            .Returns(blobRepository);

        var serviceLogger = Mock.Of<ILogger<ClipService>>();

        return new ClipService(
            contextFactory.Object,
            Mock.Of<IConfigurationService>(),
            Mock.Of<IClipboardService>(),
            Mock.Of<ITemplateService>(),
            serviceLogger);
    }

    private IExportImportService CreateExportImportService()
    {
        var logger = Mock.Of<ILogger<ExportImportService>>();
        return new ExportImportService(logger);
    }

    private static string CreateTempFilePath(string extension)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ClipMateIntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"test-{Guid.NewGuid():N}{extension}");
    }

    private static byte[] CreateTestImageData() =>
        // Minimal valid 2x2 red PNG (67 bytes)
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, // 2x2 dimensions
            0x08, 0x02, 0x00, 0x00, 0x00, 0xFD, 0xD4, 0x9A, 0x73, // RGB, CRC
            0x00, 0x00, 0x00, 0x14, 0x49, 0x44, 0x41, 0x54, // IDAT chunk
            0x78, 0x9C, 0x62, 0xF8, 0xCF, 0xC0, 0x00, 0x00, // Compressed data
            0x00, 0x06, 0x00, 0x02, 0x54, 0xEF, 0x8C, 0x82, 0x00, 0x00, // Continued
            0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82, // IEND chunk
        ];

    #endregion
}
