using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests verifying that encrypted clips have their content properly
/// loaded/decrypted before clipboard operations (not base64 encrypted data).
/// 
/// The core bug: When SetClipboardContentAsync is called with an encrypted clip,
/// the clip.TextContent/ImageData contains encrypted base64 data from BLOB tables,
/// not the decrypted plain text. This means encrypted gibberish gets pasted.
/// </summary>
public class ClipboardEncryptionIntegrationTests : IntegrationTestBase
{
    private const string TestDatabaseKey = "TestDatabase";
    private const string TestPassphrase = "MySecurePassword123!";
    private readonly ILogger<ClipboardEncryptionIntegrationTests> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ClipboardEncryptionIntegrationTests>();

    [Test]
    public async Task EncryptedClip_LoadedFromDatabase_HasEncryptedContentInTransientProperties()
    {
        // This test investigates what happens when an encrypted clip is loaded from the database.
        
        // Arrange - Create and encrypt a clip
        const string originalText = "This is my secret message!";
        var clipService = CreateClipServiceWithRealEncryption();
        
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = originalText,
            Title = "Secret Note",
            ContentHash = "test_hash_clipboard_bug",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(TestDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        using var encryptionKey = EncryptionKey.FromPassphrase(TestPassphrase);
        await clipService.EncryptClipsAsync(TestDatabaseKey, [savedClip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Check what's in the BLOB table
        var blobRepository = new BlobRepository(DbContext);
        var encryptedBlobs = await blobRepository.GetTextByClipIdAsync(savedClip.Id);
        await Assert.That(encryptedBlobs.Count).IsGreaterThan(0);
        
        var blobData = encryptedBlobs[0].Data;
        
        // Act - Load the encrypted clip (as QuickPaste would)
        var loadedClip = await clipService.GetByIdAsync(TestDatabaseKey, savedClip.Id);

        // Assert - The clip is marked as encrypted
        await Assert.That(loadedClip).IsNotNull();
        await Assert.That(loadedClip!.Encrypted).IsTrue();
        
        // Check what's in TextContent - is it plain, encrypted, or null?
        // If it contains plain text, that's a bug (encryption didn't work)
        // If it contains encrypted base64, that's the bug we're looking for
        // If it's null, that's actually correct (transient property not loaded)
        
        _logger.LogInformation("BLOB Data: {BlobData}", blobData.Length > 50 ? blobData[..50] + "..." : blobData);
        _logger.LogInformation("Clip.TextContent: {TextContent}", loadedClip.TextContent ?? "(null)");
        _logger.LogInformation("Original Text: {OriginalText}", originalText);
        
        // The BLOB should contain encrypted data (not plain text)
        await Assert.That(blobData).IsNotEqualTo(originalText);
        
        // The question: What does TextContent contain?
        // Expected: TextContent should be null (transient property not loaded from BLOB)
        // After fix: EncryptClipsAsync should clear transient properties
        
        if (loadedClip.TextContent != null)
        {
            _logger.LogError("TextContent should be null after encryption, but contains: '{TextContent}'\nBLOB Data: '{BlobData}'",
                loadedClip.TextContent,
                blobData.Length > 50 ? blobData[..50] + "..." : blobData);
        }

        await Assert.That(loadedClip.TextContent).IsNull();
    }

    [Test]
    public async Task DecryptedClip_Permanent_HasDecryptedContentInBlobTables()
    {
        // This test shows that after PERMANENT decryption, the BLOB tables contain
        // decrypted content, but the Clip entity's transient properties are still NULL
        // unless explicitly loaded.
        
        // Arrange - Create and encrypt a clip
        const string originalText = "Secret data that will be decrypted";
        var clipService = CreateClipServiceWithRealEncryption();
        
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = originalText,
            Title = "Secret Note",
            ContentHash = "test_hash_decrypt_permanent",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(TestDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        using var encryptionKey = EncryptionKey.FromPassphrase(TestPassphrase);
        await clipService.EncryptClipsAsync(TestDatabaseKey, [savedClip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Act - Decrypt permanently
        await clipService.DecryptClipsAsync(TestDatabaseKey, [savedClip.Id], encryptionKey, isPermanent: true);
        await DbContext.SaveChangesAsync();

        // Load the decrypted clip
        var decryptedClip = await clipService.GetByIdAsync(TestDatabaseKey, savedClip.Id);

        // Assert - Clip is no longer marked as encrypted
        await Assert.That(decryptedClip).IsNotNull();
        await Assert.That(decryptedClip!.Encrypted).IsFalse();
        
        // STILL: The transient TextContent is NULL (not auto-populated)
        await Assert.That(decryptedClip.TextContent).IsNull();
        
        // The decrypted data IS in the BLOB tables
        var blobRepository = new BlobRepository(DbContext);
        var decryptedBlobs = await blobRepository.GetTextByClipIdAsync(savedClip.Id);
        await Assert.That(decryptedBlobs.Count).IsGreaterThan(0);
        await Assert.That(decryptedBlobs[0].Data).IsEqualTo(originalText); // Decrypted!
    }

    [Test]
    public async Task SetClipboardContentAsync_WithEncryptedClip_ShouldFailOrWarn()
    {
        // This test documents the expected behavior: SetClipboardContentAsync should
        // detect that the clip is encrypted and either:
        // 1. Throw an exception
        // 2. Return false/fail gracefully
        // 3. Load the decrypted content if available in cache
        //
        // It should NOT blindly copy null/empty content to the clipboard.
        
        // Arrange - Create and encrypt a clip  
        const string originalText = "This should not be copied as encrypted data";
        var clipService = CreateClipServiceWithRealEncryption();
        
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = originalText,
            Title = "Secret Note",
            ContentHash = "test_hash_clipboard_safety",
            CapturedAt = DateTime.UtcNow,
            CollectionId = Guid.Empty,
        };

        var savedClip = await clipService.CreateAsync(TestDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        using var encryptionKey = EncryptionKey.FromPassphrase(TestPassphrase);
        await clipService.EncryptClipsAsync(TestDatabaseKey, [savedClip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Load encrypted clip (TextContent will be null)
        var encryptedClip = await clipService.GetByIdAsync(TestDatabaseKey, savedClip.Id);
        await Assert.That(encryptedClip).IsNotNull();
        await Assert.That(encryptedClip!.Encrypted).IsTrue();
        await Assert.That(encryptedClip.TextContent).IsNull();

        // Act & Assert - Attempting to set clipboard with encrypted clip should be handled safely
        // TODO: Once SetClipboardContentAsync is fixed, this should either:
        // - Throw an InvalidOperationException("Cannot copy encrypted clip to clipboard")
        // - Check for decrypted content in cache and use that
        // - Return false to indicate failure
        
        // For now, this test documents the problem - we can't test clipboard directly in unit tests
        // but the issue is that encryptedClip.TextContent is null, so clipboard would be empty/fail
    }

    // Helper Methods

    private IClipService CreateClipServiceWithRealEncryption()
    {
        var contextFactory = new Mock<IDatabaseContextFactory>();

        var clipLogger = Mock.Of<ILogger<ClipRepository>>();
        var clipRepository = new ClipRepository(DbContext, clipLogger);
        contextFactory.Setup(p => p.GetClipRepository(TestDatabaseKey))
            .Returns(clipRepository);

        var clipDataRepository = new ClipDataRepository(DbContext);
        contextFactory.Setup(p => p.GetClipDataRepository(TestDatabaseKey))
            .Returns(clipDataRepository);

        var blobRepository = new BlobRepository(DbContext);
        contextFactory.Setup(p => p.GetBlobRepository(TestDatabaseKey))
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
            Mock.Of<CommunityToolkit.Mvvm.Messaging.IMessenger>(),
            serviceLogger);
    }
}
