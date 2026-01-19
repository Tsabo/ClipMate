using System.Xml.Serialization;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Export;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for XML export/import with real database persistence.
/// Verifies that clips saved to database can be exported with all blob data intact.
/// 
/// ENCRYPTION EXPORT/IMPORT BEHAVIOR:
/// - Encrypted clips store encrypted data in normal content fields (TextContent, RtfContent, HtmlContent, ImageData)
/// - TEXT fields: Encrypted data is base64-encoded (e.g., TextContent = "QUJDREVGMTIzNDU2Nzg5MA==")
/// - IMAGE fields: Encrypted data is raw bytes (e.g., ImageData = byte[] { 0x01, 0x02, 0xAA, 0xBB })
/// - Export: Encrypted content is exported as-is in the normal fields
/// - Import: When Encrypted=true, the fields contain encrypted data ready for decryption
/// - Multiple formats: Each format (Text/RTF/HTML/Image) is encrypted separately and stored in its own field
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

        foreach (var item in clips)
            await clipService.CreateAsync(_testDatabaseKey, item);

        await DbContext.SaveChangesAsync();

        // Load clips back
        var loadedClips = await clipService.GetByCollectionAsync(_testDatabaseKey, Guid.Empty);

        // Load blob data for all clips
        foreach (var item in loadedClips)
            await clipService.LoadBlobDataAsync(_testDatabaseKey, item);

        // Act - Export all clips
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync(loadedClips, [], xmlPath);

        // Assert - Verify all clips have their content
        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        await Assert.That(importData.Clips.Count).IsEqualTo(3);

        var textClip = importData.Clips.First(p => p.Type == ClipType.Text);
        await Assert.That(textClip.TextContent).IsEqualTo("Text clip content");

        var htmlClip = importData.Clips.First(p => p.Type == ClipType.Html);
        await Assert.That(htmlClip.HtmlContent).IsEqualTo("<b>HTML</b>");

        var imageClip = importData.Clips.First(p => p.Type == ClipType.Image);
        await Assert.That(imageClip.ImageData!).IsNotNull();
    }

    [Test]
    public async Task ExportImport_WithEncryptedClip_PreservesEncryptionProperties()
    {
        // Arrange - Create encrypted clip with encrypted data in normal content fields
        // When content is encrypted, it's stored as base64 in TextContent/RtfContent/HtmlContent
        // and as encrypted bytes in ImageData
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            // Encrypted text content is stored as base64-encoded encrypted data
            TextContent = "QUJDREVGMTIzNDU2Nzg5MA==", // Simulated base64-encoded encrypted text
            RtfContent = "cnRmX2VuY3J5cHRlZF9kYXRh", // Simulated base64-encoded encrypted RTF
            HtmlContent = "aHRtbF9lbmNyeXB0ZWRfZGF0YQ==", // Simulated base64-encoded encrypted HTML
            Title = "Encrypted Clip",
            ContentHash = "test_hash_encrypted",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
            Encrypted = true,
            EncryptionSalt = "test_salt_base64_encoded_value",
            EncryptionIv = "test_iv_base64_encoded_value",
            EncryptionMethod = "AES256-GCM",
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Act - Export to XML (encrypted data is in TextContent/RtfContent/HtmlContent)
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync([savedClip], [], xmlPath);

        // Verify XML contains encryption properties AND encrypted content
        var xmlContent = await File.ReadAllTextAsync(xmlPath);
        await Assert.That(xmlContent).Contains("<Encrypted>true</Encrypted>");
        await Assert.That(xmlContent).Contains("<EncryptionSalt>test_salt_base64_encoded_value</EncryptionSalt>");
        await Assert.That(xmlContent).Contains("<EncryptionIv>test_iv_base64_encoded_value</EncryptionIv>");
        await Assert.That(xmlContent).Contains("<EncryptionMethod>AES256-GCM</EncryptionMethod>");
        // Verify encrypted content is exported in normal fields
        await Assert.That(xmlContent).Contains("<TextContent>QUJDREVGMTIzNDU2Nzg5MA==</TextContent>");
        await Assert.That(xmlContent).Contains("<RtfContent>cnRmX2VuY3J5cHRlZF9kYXRh</RtfContent>");
        await Assert.That(xmlContent).Contains("<HtmlContent>aHRtbF9lbmNyeXB0ZWRfZGF0YQ==</HtmlContent>");

        // Import from XML
        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        await Assert.That(importData.Clips).Count().IsEqualTo(1);

        var importedClip = importData.Clips[0];

        // Assert - All encryption properties AND encrypted content preserved
        await Assert.That(importedClip.Encrypted).IsTrue();
        await Assert.That(importedClip.EncryptionSalt).IsEqualTo("test_salt_base64_encoded_value");
        await Assert.That(importedClip.EncryptionIv).IsEqualTo("test_iv_base64_encoded_value");
        await Assert.That(importedClip.EncryptionMethod).IsEqualTo("AES256-GCM");
        // Verify encrypted content is imported (base64-encoded encrypted data)
        await Assert.That(importedClip.TextContent).IsEqualTo("QUJDREVGMTIzNDU2Nzg5MA==");
        await Assert.That(importedClip.RtfContent).IsEqualTo("cnRmX2VuY3J5cHRlZF9kYXRh");
        await Assert.That(importedClip.HtmlContent).IsEqualTo("aHRtbF9lbmNyeXB0ZWRfZGF0YQ==");
        
        // With the encrypted content and crypto parameters, user can decrypt with correct passphrase
    }

    [Test]
    public async Task ExportImport_WithEncryptedImageClip_PreservesEncryptedImageData()
    {
        // Arrange - Create encrypted image clip with encrypted image data
        var clipService = CreateClipService();
        var encryptedImageBytes = new byte[] { 0x01, 0x02, 0x03, 0xAA, 0xBB, 0xCC }; // Simulated encrypted image bytes
        var clip = new Clip
        {
            Type = ClipType.Image,
            ImageData = encryptedImageBytes, // Encrypted image stored as raw bytes
            Title = "Encrypted Image",
            ContentHash = "test_hash_encrypted_image",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
            Encrypted = true,
            EncryptionSalt = "image_salt_value",
            EncryptionIv = "image_iv_value",
            EncryptionMethod = "AES256-GCM",
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Act - Export to XML (encrypted image data is in ImageData as bytes, exported as base64)
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync([savedClip], [], xmlPath);

        // Verify XML contains encryption properties and encrypted image data as base64
        var xmlContent = await File.ReadAllTextAsync(xmlPath);
        await Assert.That(xmlContent).Contains("<Encrypted>true</Encrypted>");
        await Assert.That(xmlContent).Contains("<ImageDataBase64>"); // Encrypted bytes exported as base64

        // Import from XML
        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        await Assert.That(importData.Clips).Count().IsEqualTo(1);

        var importedClip = importData.Clips[0];

        // Assert - Encrypted image data preserved
        await Assert.That(importedClip.Encrypted).IsTrue();
        await Assert.That(importedClip.ImageData).IsNotNull();
        await Assert.That(importedClip.ImageData).IsEquivalentTo(encryptedImageBytes);
    }

    [Test]
    public async Task ExportImport_WithUnencryptedClip_DoesNotSetEncryptionProperties()
    {
        // Arrange - Create regular unencrypted clip
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Plain text content",
            Title = "Regular Clip",
            ContentHash = "test_hash_plain",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
            Encrypted = false,
            EncryptionSalt = null,
            EncryptionIv = null,
            EncryptionMethod = null,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Act - Export and import (use savedClip which has TextContent in memory)
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync([savedClip], [], xmlPath);

        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        var importedClip = importData.Clips[0];

        // Assert - No encryption properties set
        await Assert.That(importedClip.Encrypted).IsFalse();
        await Assert.That(importedClip.EncryptionSalt).IsNull();
        await Assert.That(importedClip.EncryptionIv).IsNull();
        await Assert.That(importedClip.EncryptionMethod).IsNull();
        await Assert.That(importedClip.TextContent).IsEqualTo("Plain text content");
    }

    [Test]
    public async Task EncryptExportImportDecrypt_FullCycle_RestoresOriginalContent()
    {
        // Arrange - Create a clip with plain text content
        var clipService = CreateClipServiceWithRealEncryption();
        const string originalText = "This is my secret message that will be encrypted!";
        const string originalRtf = @"{\rtf1\ansi This is RTF content}";
        const string originalHtml = "<p>This is <b>HTML</b> content</p>";
        const string testPassphrase = "MySecurePassword123!";

        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = originalText,
            RtfContent = originalRtf,
            HtmlContent = originalHtml,
            Title = "My Secret Note",
            ContentHash = "test_hash_cycle",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Act 1 - Encrypt the clip
        using var encryptionKey = EncryptionKey.FromPassphrase(testPassphrase);
        await clipService.EncryptClipsAsync(_testDatabaseKey, [savedClip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Verify encryption worked
        var encryptedClip = await clipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(encryptedClip).IsNotNull();
        await Assert.That(encryptedClip!.Encrypted).IsTrue();
        
        // Verify BLOB data is encrypted (not plain text)
        var blobRepository = new BlobRepository(DbContext);
        var textBlobs = await blobRepository.GetTextByClipIdAsync(savedClip.Id);
        await Assert.That(textBlobs.Count).IsGreaterThan(0);
        await Assert.That(textBlobs[0].Data).IsNotEqualTo(originalText); // Should be base64 encrypted

        // Act 2 - Export the encrypted clip to XML
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync([encryptedClip], [], xmlPath);

        // Verify XML contains encrypted data
        var xmlContent = await File.ReadAllTextAsync(xmlPath);
        await Assert.That(xmlContent).Contains("<Encrypted>true</Encrypted>");
        await Assert.That(xmlContent).Contains("<EncryptionSalt>");
        await Assert.That(xmlContent).Contains("<EncryptionIv>");
        await Assert.That(xmlContent).Contains("<EncryptionMethod>AES-256</EncryptionMethod>");

        // Act 3 - Import the encrypted clip from XML into a new database context
        var connection = DbContext.Database.GetDbConnection();
        using var importContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);

        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        var importedClip = importData.Clips[0];

        // Save imported clip to database
        var importClipService = CreateClipServiceWithRealEncryptionAndContext(importContext);
        var reimportedClip = await importClipService.CreateAsync(_testDatabaseKey, importedClip);
        await importContext.SaveChangesAsync();

        // Verify imported clip is still encrypted
        await Assert.That(reimportedClip.Encrypted).IsTrue();
        await Assert.That(reimportedClip.EncryptionSalt).IsNotEmpty();
        await Assert.That(reimportedClip.EncryptionIv).IsNotEmpty();

        // Act 4 - Decrypt the imported clip with the same passphrase
        using var decryptionKey = EncryptionKey.FromPassphrase(testPassphrase);
        await importClipService.DecryptClipsAsync(_testDatabaseKey, [reimportedClip.Id], decryptionKey, isPermanent: true);
        await importContext.SaveChangesAsync();

        // Assert - Verify decrypted content matches original (read from BLOB table)
        var decryptedClip = await importClipService.GetByIdAsync(_testDatabaseKey, reimportedClip.Id);
        await Assert.That(decryptedClip).IsNotNull();
        await Assert.That(decryptedClip!.Encrypted).IsFalse(); // Permanently decrypted
        
        // Verify BLOB data was decrypted back to original
        // BlobTxt entries contain text content for various formats (plain text, RTF, HTML)
        var importBlobRepo = new BlobRepository(importContext);
        var decryptedTextBlobs = await importBlobRepo.GetTextByClipIdAsync(reimportedClip.Id);
        await Assert.That(decryptedTextBlobs.Count).IsGreaterThan(0);
        
        // Verify at least one blob contains our original text (typically the first one is plain text)
        var hasOriginalText = decryptedTextBlobs.Any(b => b.Data == originalText);
        await Assert.That(hasOriginalText).IsTrue();
    }

    [Test]
    public async Task EncryptExportImportDecrypt_WithImageContent_RestoresOriginalImage()
    {
        // Arrange - Create a clip with image data
        var clipService = CreateClipServiceWithRealEncryption();
        var originalImageBytes = CreateTestImageData();
        const string testPassphrase = "ImagePassword456!";

        var clip = new Clip
        {
            Type = ClipType.Image,
            ImageData = originalImageBytes,
            Title = "My Secret Screenshot",
            ContentHash = "test_hash_image_cycle",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Act 1 - Encrypt the image clip
        using var encryptionKey = EncryptionKey.FromPassphrase(testPassphrase);
        await clipService.EncryptClipsAsync(_testDatabaseKey, [savedClip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Verify encryption
        var encryptedClip = await clipService.GetByIdAsync(_testDatabaseKey, savedClip.Id);
        await Assert.That(encryptedClip).IsNotNull();
        await Assert.That(encryptedClip!.Encrypted).IsTrue();
        
        // Verify BLOB data is encrypted (not plain image bytes)
        var blobRepository = new BlobRepository(DbContext);
        var pngBlobs = await blobRepository.GetPngByClipIdAsync(savedClip.Id);
        await Assert.That(pngBlobs.Count).IsGreaterThan(0);
        await Assert.That(pngBlobs[0].Data).IsNotEquivalentTo(originalImageBytes); // Should be encrypted bytes

        // Act 2 - Export, import, decrypt cycle
        var exportService = CreateExportImportService();
        var xmlPath = CreateTempFilePath(".xml");
        await exportService.ExportToXmlAsync([encryptedClip], [], xmlPath);

        var importData = await exportService.ImportFromXmlAsync(xmlPath);
        var importedClip = importData.Clips[0];

        var connection = DbContext.Database.GetDbConnection();
        using var importContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);

        var importClipService = CreateClipServiceWithRealEncryptionAndContext(importContext);
        var reimportedClip = await importClipService.CreateAsync(_testDatabaseKey, importedClip);
        await importContext.SaveChangesAsync();

        // Decrypt with same passphrase
        using var decryptionKey = EncryptionKey.FromPassphrase(testPassphrase);
        await importClipService.DecryptClipsAsync(_testDatabaseKey, [reimportedClip.Id], decryptionKey, isPermanent: true);
        await importContext.SaveChangesAsync();

        // Assert - Verify decrypted image matches original (read from BLOB table)
        var decryptedClip = await importClipService.GetByIdAsync(_testDatabaseKey, reimportedClip.Id);
        await Assert.That(decryptedClip).IsNotNull();
        await Assert.That(decryptedClip!.Encrypted).IsFalse();
        
        // Verify BLOB data was decrypted back to original image bytes
        var importBlobRepo = new BlobRepository(importContext);
        var decryptedPngBlobs = await importBlobRepo.GetPngByClipIdAsync(reimportedClip.Id);
        await Assert.That(decryptedPngBlobs.Count).IsGreaterThan(0);
        await Assert.That(decryptedPngBlobs[0].Data).IsEquivalentTo(originalImageBytes);
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
            Mock.Of<IEncryptionService>(),
            Mock.Of<IDecryptedBlobCacheService>(),
            Mock.Of<IMessenger>(),
            serviceLogger);
    }

    private IClipService CreateClipServiceWithRealEncryption()
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

        // Use real encryption service
        var encryptionService = new AesEncryptionService();

        // Use real decrypted blob cache service
#pragma warning disable CA2000 // Dispose objects before losing scope - test objects don't require explicit disposal
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheLogger = Mock.Of<ILogger<DecryptedBlobCacheService>>();
        var decryptedBlobCacheService = new DecryptedBlobCacheService(memoryCache, cacheLogger);
#pragma warning restore CA2000

        var serviceLogger = Mock.Of<ILogger<ClipService>>();

        return new ClipService(
            contextFactory.Object,
            Mock.Of<IConfigurationService>(),
            Mock.Of<IClipboardService>(),
            Mock.Of<ITemplateService>(),
            encryptionService,
            decryptedBlobCacheService,
            Mock.Of<IMessenger>(),
            serviceLogger);
    }

    private IClipService CreateClipServiceWithRealEncryptionAndContext(ClipMateDbContext context)
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

        // Use real encryption service
        var encryptionService = new AesEncryptionService();

        // Use real decrypted blob cache service
#pragma warning disable CA2000 // Dispose objects before losing scope - test objects don't require explicit disposal
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheLogger = Mock.Of<ILogger<DecryptedBlobCacheService>>();
        var decryptedBlobCacheService = new DecryptedBlobCacheService(memoryCache, cacheLogger);
#pragma warning restore CA2000

        var serviceLogger = Mock.Of<ILogger<ClipService>>();

        return new ClipService(
            contextFactory.Object,
            Mock.Of<IConfigurationService>(),
            Mock.Of<IClipboardService>(),
            Mock.Of<ITemplateService>(),
            encryptionService,
            decryptedBlobCacheService,
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
            Mock.Of<IEncryptionService>(), Mock.Of<IDecryptedBlobCacheService>(),
            Mock.Of<IMessenger>(), serviceLogger);
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
