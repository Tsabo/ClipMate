using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Export;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for flat file export with real database persistence.
/// Verifies that clips saved to database can be exported to individual files with all content intact.
/// </summary>
public class FlatFileExportIntegrationTests : IntegrationTestBase
{
    private const string _testDatabaseKey = "db_test0001";

    [Test]
    public async Task ExportClipsToFiles_WithTextClipFromDatabase_ExportsCorrectContent()
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

        // Verify blob data is NOT loaded initially (real-world scenario)
        await Assert.That(loadedClip?.TextContent).IsNull();

        // Act - Export directly without manually loading blob data
        // The export service should load blob data automatically
        var exportService = CreateExportImportService();
        var exportDir = CreateTempDirectory();

        await exportService.ExportClipsToFilesAsync(
            _testDatabaseKey,
            [loadedClip!],
            exportDir,
            FileNamingStrategy.Sequential);

        // Assert - Verify file was created with correct content
        var files = Directory.GetFiles(exportDir);
        await Assert.That(files.Length).IsEqualTo(1);

        var exportedContent = await File.ReadAllTextAsync(files[0]);
        await Assert.That(exportedContent).IsEqualTo("Hello World from Database");

        // Cleanup
        await using (newContext) { }
    }

    [Test]
    public async Task ExportClipsToFiles_WithMultiLineTextClip_ExportsAllLines()
    {
        // Arrange
        var clipService = CreateClipService();
        const string multiLineText = "Line 1\nLine 2\nLine 3\nLine 4";
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = multiLineText,
            Title = "Multi-line Clip",
            ContentHash = "test_hash_multiline",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Load clip back (without blob data)
        var connection = DbContext.Database.GetDbConnection();
        var newContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);

        var newClipService = CreateClipServiceWithContext(newContext);
        var loadedClip = await newClipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);

        // Act - Export
        var exportService = CreateExportImportService();
        var exportDir = CreateTempDirectory();

        await exportService.ExportClipsToFilesAsync(
            _testDatabaseKey,
            [loadedClip!],
            exportDir,
            FileNamingStrategy.TitleBased);

        // Assert - All lines should be present
        var files = Directory.GetFiles(exportDir);
        await Assert.That(files.Length).IsEqualTo(1);

        var exportedContent = await File.ReadAllTextAsync(files[0]);
        await Assert.That(exportedContent).IsEqualTo(multiLineText);
        await Assert.That(exportedContent).Contains("Line 1");
        await Assert.That(exportedContent).Contains("Line 2");
        await Assert.That(exportedContent).Contains("Line 3");
        await Assert.That(exportedContent).Contains("Line 4");

        // Cleanup
        await using (newContext) { }
    }

    [Test]
    public async Task ExportClipsToFiles_WithHtmlClipFromDatabase_ExportsHtmlContent()
    {
        // Arrange
        var clipService = CreateClipService();
        const string htmlContent = "<html><body><h1>Test HTML</h1><p>Content here</p></body></html>";
        var clip = new Clip
        {
            Type = ClipType.Html,
            HtmlContent = htmlContent,
            TextContent = "Test HTML Content here",
            Title = "Test HTML Clip",
            ContentHash = "test_hash_html",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Load clip back
        var connection = DbContext.Database.GetDbConnection();
        var newContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);

        var newClipService = CreateClipServiceWithContext(newContext);
        var loadedClip = await newClipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(loadedClip?.HtmlContent).IsNull(); // Not loaded initially

        // Act
        var exportService = CreateExportImportService();
        var exportDir = CreateTempDirectory();

        await exportService.ExportClipsToFilesAsync(
            _testDatabaseKey,
            [loadedClip!],
            exportDir,
            FileNamingStrategy.Serial);

        // Assert
        var files = Directory.GetFiles(exportDir, "*.html");
        await Assert.That(files.Length).IsEqualTo(1);

        var exportedContent = await File.ReadAllTextAsync(files[0]);
        await Assert.That(exportedContent).IsEqualTo(htmlContent);

        // Cleanup
        await using (newContext) { }
    }

    [Test]
    public async Task ExportClipsToFiles_WithImageClipFromDatabase_ExportsImageData()
    {
        // Arrange
        var clipService = CreateClipService();

        // Create a simple 2x2 red PNG
        var pngData = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02,
            0x08, 0x02, 0x00, 0x00, 0x00, 0xFD, 0xD4, 0x9A, 0x73,
            0x00, 0x00, 0x00, 0x14, 0x49, 0x44, 0x41, 0x54,
            0x78, 0x9C, 0x62, 0xF8, 0xCF, 0xC0, 0x00, 0x00,
            0x00, 0x06, 0x00, 0x02, 0x54, 0xEF, 0x8C, 0x82, 0x00, 0x00,
            0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
        };

        var clip = new Clip
        {
            Type = ClipType.Image,
            ImageData = pngData,
            Title = "Test Image Clip",
            ContentHash = "test_hash_image",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Load clip back
        var connection = DbContext.Database.GetDbConnection();
        var newContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);

        var newClipService = CreateClipServiceWithContext(newContext);
        var loadedClip = await newClipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(loadedClip?.ImageData).IsNull(); // Not loaded initially

        // Act
        var exportService = CreateExportImportService();
        var exportDir = CreateTempDirectory();

        await exportService.ExportClipsToFilesAsync(
            _testDatabaseKey,
            [loadedClip!],
            exportDir,
            FileNamingStrategy.TitleBased,
            imageFormat: ImageExportFormat.Png);

        // Assert
        var files = Directory.GetFiles(exportDir, "*.png");
        await Assert.That(files.Length).IsEqualTo(1);

        var exportedData = await File.ReadAllBytesAsync(files[0]);
        await Assert.That(exportedData.Length).IsGreaterThan(0);
        // Verify PNG signature
        await Assert.That(exportedData[0]).IsEqualTo((byte)0x89);
        await Assert.That(exportedData[1]).IsEqualTo((byte)0x50);
        await Assert.That(exportedData[2]).IsEqualTo((byte)0x4E);
        await Assert.That(exportedData[3]).IsEqualTo((byte)0x47);

        // Cleanup
        await using (newContext) { }
    }

    [Test]
    public async Task ExportClipsToFiles_WithMultipleClipsFromDatabase_ExportsAll()
    {
        // Arrange
        var clipService = CreateClipService();
        var clips = new[]
        {
            new Clip
            {
                Type = ClipType.Text,
                TextContent = "First clip content",
                Title = "Clip 1",
                ContentHash = "hash_1",
                CapturedAt = DateTime.UtcNow,
                CollectionId = Guid.Empty,
            },
            new Clip
            {
                Type = ClipType.Text,
                TextContent = "Second clip content",
                Title = "Clip 2",
                ContentHash = "hash_2",
                CapturedAt = DateTime.UtcNow,
                CollectionId = Guid.Empty,
            },
            new Clip
            {
                Type = ClipType.Text,
                TextContent = "Third clip content",
                Title = "Clip 3",
                ContentHash = "hash_3",
                CapturedAt = DateTime.UtcNow,
                CollectionId = Guid.Empty,
            },
        };

        var savedClips = new List<Clip>();
        foreach (var clip in clips)
        {
            var saved = await clipService.CreateAsync(_testDatabaseKey, clip);
            savedClips.Add(saved);
        }

        await DbContext.SaveChangesAsync();

        // Load clips back
        var connection = DbContext.Database.GetDbConnection();
        var newContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);

        var newClipService = CreateClipServiceWithContext(newContext);
        var loadedClips = new List<Clip>();
        foreach (var saved in savedClips)
        {
            var loaded = await newClipService.GetByIdAsync(_testDatabaseKey, saved.Id);
            loadedClips.Add(loaded!);
        }

        // Act
        var exportService = CreateExportImportService();
        var exportDir = CreateTempDirectory();

        await exportService.ExportClipsToFilesAsync(
            _testDatabaseKey,
            loadedClips,
            exportDir,
            FileNamingStrategy.Sequential);

        // Assert
        var files = Directory.GetFiles(exportDir).OrderBy(f => f).ToArray();
        await Assert.That(files.Length).IsEqualTo(3);

        var content1 = await File.ReadAllTextAsync(files[0]);
        var content2 = await File.ReadAllTextAsync(files[1]);
        var content3 = await File.ReadAllTextAsync(files[2]);

        await Assert.That(content1).IsEqualTo("First clip content");
        await Assert.That(content2).IsEqualTo("Second clip content");
        await Assert.That(content3).IsEqualTo("Third clip content");

        // Cleanup
        await using (newContext) { }
    }

    [Test]
    public async Task ExportClipsToFiles_WithEmptyTextContent_FallsBackToTitle()
    {
        // Arrange - Clip with no TextContent but has Title
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            // TextContent intentionally not set
            Title = "Fallback Title",
            ContentHash = "test_hash_fallback",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Load clip back
        var connection = DbContext.Database.GetDbConnection();
        var newContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);

        var newClipService = CreateClipServiceWithContext(newContext);
        var loadedClip = await newClipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);

        // Act
        var exportService = CreateExportImportService();
        var exportDir = CreateTempDirectory();

        await exportService.ExportClipsToFilesAsync(
            _testDatabaseKey,
            [loadedClip!],
            exportDir,
            FileNamingStrategy.TitleBased);

        // Assert - Should use Title as fallback content
        var files = Directory.GetFiles(exportDir);
        await Assert.That(files.Length).IsEqualTo(1);

        var exportedContent = await File.ReadAllTextAsync(files[0]);
        await Assert.That(exportedContent).IsEqualTo("Fallback Title");

        // Cleanup
        await using (newContext) { }
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ClipMateIntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
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
            Mock.Of<IEncryptionService>(),
            Mock.Of<IDecryptedBlobCacheService>(),
            Mock.Of<IMessenger>(),
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
            Mock.Of<IEncryptionService>(),
            Mock.Of<IDecryptedBlobCacheService>(),
            Mock.Of<IMessenger>(),
            serviceLogger);
    }

    private IExportImportService CreateExportImportService()
    {
        var clipService = CreateClipService();
        var logger = Mock.Of<ILogger<ExportImportService>>();
        return new ExportImportService(clipService, logger);
    }
}
