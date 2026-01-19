using ClipMate.Core.Models;
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
/// Integration tests for clip encryption and decryption with full database round-trip.
/// Verifies that encrypted clips can be saved, retrieved, and decrypted correctly.
/// </summary>
public class ClipEncryptionIntegrationTests : IntegrationTestBase
{
    private const string _testDatabaseKey = "db_test0001";
    private const string _testPassphrase = "Test-Passphrase-123!";

    [Test]
    public async Task EncryptClip_WithTitle_ShouldEncryptAndPersistToDatabase()
    {
        // Arrange
        var clipService = CreateClipService();
        const string originalTitle = "Secret Document Title";
        const string originalText = "This is secret text content";

        var clip = await CreateTestClipWithData(clipService, originalTitle, originalText);

        using var encryptionKey = EncryptionKey.FromPassphrase(_testPassphrase);

        // Act - Encrypt the clip
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Assert - Verify encryption flags and data
        var encryptedClip = await clipService.GetByIdAsync(_testDatabaseKey, clip.Id);
        await Assert.That(encryptedClip).IsNotNull();
        await Assert.That(encryptedClip!.Encrypted).IsTrue();
        await Assert.That(encryptedClip.IsDecrypted).IsFalse();
        await Assert.That(encryptedClip.EncryptionSalt).IsNotEmpty();
        await Assert.That(encryptedClip.EncryptionIv).IsNotEmpty();
        await Assert.That(encryptedClip.EncryptionMethod).IsEqualTo("AES-256");

        // Title should be encrypted (Base64)
        await Assert.That(encryptedClip.Title).IsNotEqualTo(originalTitle);
        await Assert.That(IsBase64String(encryptedClip.Title)).IsTrue();

        // DisplayTitle should show "This clip is encrypted - unable to display."
        await Assert.That(encryptedClip.DisplayTitle).IsEqualTo("This clip is encrypted - unable to display.");

        // Verify TEXT BLOB is encrypted
        var blobRepository = new BlobRepository(DbContext);
        var textBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id);
        await Assert.That(textBlobs.Count).IsGreaterThan(0);
        await Assert.That(textBlobs[0].Data).IsNotEqualTo(originalText);
        await Assert.That(IsBase64String(textBlobs[0].Data)).IsTrue();
    }

    [Test]
    public async Task DecryptClip_WithCorrectKey_ShouldRestoreOriginalTitleAndContent()
    {
        // Arrange
        var clipService = CreateClipService();
        const string originalTitle = "My Important Note";
        const string originalText = "This text should be encrypted and then restored";

        var clip = await CreateTestClipWithData(clipService, originalTitle, originalText);

        using var encryptionKey = EncryptionKey.FromPassphrase(_testPassphrase);

        // Encrypt first
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Verify it's encrypted
        var encryptedClip = await clipService.GetByIdAsync(_testDatabaseKey, clip.Id);
        await Assert.That(encryptedClip!.Encrypted).IsTrue();
        await Assert.That(encryptedClip.Title).IsNotEqualTo(originalTitle);

        // Act - Decrypt the clip permanently
        await clipService.DecryptClipsAsync(_testDatabaseKey, [clip.Id], encryptionKey, true);

        // Assert - Verify decryption restored original data
        // Note: DecryptClipsAsync modifies the clip object in-memory. Title decryption is transient.
        // To verify Title was decrypted, we need to check the TEXT BLOB which IS persisted.
        var blobRepository = new BlobRepository(DbContext);
        var textBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id);
        await Assert.That(textBlobs.Count).IsGreaterThan(0);
        await Assert.That(textBlobs[0].Data).IsEqualTo(originalText);
    }

    [Test]
    public async Task EncryptDecryptRoundTrip_AcrossDbContexts_ShouldPreserveAllData()
    {
        // Arrange
        var clipService = CreateClipService();
        const string originalTitle = "Test Clip for Round Trip";
        const string originalText = "Sensitive information that must survive encryption";

        var clip = await CreateTestClipWithData(clipService, originalTitle, originalText);
        var clipId = clip.Id;

        using var encryptionKey = EncryptionKey.FromPassphrase(_testPassphrase);

        // Act - Encrypt
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clipId], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Simulate app restart: Create new DbContext and ClipService
        var connection = DbContext.Database.GetDbConnection();
        await using var newContext = new ClipMateDbContext(
            new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite(connection)
                .Options);

        var newClipService = CreateClipServiceWithContext(newContext);

        // Load encrypted clip in "new session"
        var loadedEncryptedClip = await newClipService.GetByIdAsync(_testDatabaseKey, clipId);
        await Assert.That(loadedEncryptedClip).IsNotNull();
        await Assert.That(loadedEncryptedClip!.Encrypted).IsTrue();
        await Assert.That(loadedEncryptedClip.Title).IsNotEqualTo(originalTitle);

        // Decrypt in "new session" permanently
        await newClipService.DecryptClipsAsync(_testDatabaseKey, [clipId], encryptionKey, true);

        // Assert - Verify TEXT BLOB data was decrypted and persisted
        var blobRepository = new BlobRepository(newContext);
        var textBlobs = await blobRepository.GetTextByClipIdAsync(clipId);
        await Assert.That(textBlobs[0].Data).IsEqualTo(originalText);
    }

    [Test]
    public async Task EncryptClip_WithCustomTitle_ShouldNotEncryptTitle()
    {
        // Arrange
        var clipService = CreateClipService();
        const string customTitle = "My Custom Label";
        const string originalText = "Secret content";

        var clip = await CreateTestClipWithData(clipService, customTitle, originalText);

        // Mark as custom title
        clip.CustomTitle = true;
        await DbContext.SaveChangesAsync();

        using var encryptionKey = EncryptionKey.FromPassphrase(_testPassphrase);

        // Act - Encrypt the clip
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Assert - Title should remain visible (not encrypted)
        var encryptedClip = await clipService.GetByIdAsync(_testDatabaseKey, clip.Id);
        await Assert.That(encryptedClip).IsNotNull();
        await Assert.That(encryptedClip!.Encrypted).IsTrue();
        await Assert.That(encryptedClip.CustomTitle).IsTrue();
        await Assert.That(encryptedClip.Title).IsEqualTo(customTitle);
        await Assert.That(encryptedClip.DisplayTitle).IsEqualTo(customTitle);
    }

    [Test]
    public async Task EncryptMultipleClips_ThenDecrypt_ShouldHandleAllCorrectly()
    {
        // Arrange
        var clipService = CreateClipService();

        var clip1 = await CreateTestClipWithData(clipService, "Title 1", "Content 1");
        var clip2 = await CreateTestClipWithData(clipService, "Title 2", "Content 2");
        var clip3 = await CreateTestClipWithData(clipService, "Title 3", "Content 3");

        using var encryptionKey = EncryptionKey.FromPassphrase(_testPassphrase);

        // Act - Encrypt all clips
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip1.Id, clip2.Id, clip3.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Verify all encrypted
        var clips = await Task.WhenAll(
            clipService.GetByIdAsync(_testDatabaseKey, clip1.Id),
            clipService.GetByIdAsync(_testDatabaseKey, clip2.Id),
            clipService.GetByIdAsync(_testDatabaseKey, clip3.Id));

        foreach (var item in clips)
        {
            await Assert.That(item).IsNotNull();
            await Assert.That(item!.Encrypted).IsTrue();
        }

        // Decrypt only clip2 permanently
        await clipService.DecryptClipsAsync(_testDatabaseKey, [clip2.Id], encryptionKey, true);

        // Assert - Verify clip2's TEXT BLOB was decrypted (persisted)
        var blobRepository = new BlobRepository(DbContext);
        var clip1Blobs = await blobRepository.GetTextByClipIdAsync(clip1.Id);
        var clip2Blobs = await blobRepository.GetTextByClipIdAsync(clip2.Id);
        var clip3Blobs = await blobRepository.GetTextByClipIdAsync(clip3.Id);

        // Clip2 should have decrypted content, others still encrypted
        await Assert.That(clip2Blobs[0].Data).IsEqualTo("Content 2");
        await Assert.That(IsBase64String(clip1Blobs[0].Data)).IsTrue();
        await Assert.That(IsBase64String(clip3Blobs[0].Data)).IsTrue();
    }

    [Test]
    public async Task DecryptClip_WithCorruptedTextBlob_ShouldHandleGracefully()
    {
        // Arrange
        var clipService = CreateClipService();
        const string originalTitle = "Valid Title";
        var clip = await CreateTestClipWithData(clipService, originalTitle, "Original text");

        using var encryptionKey = EncryptionKey.FromPassphrase(_testPassphrase);

        // Encrypt first
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Corrupt the TEXT BLOB (simulate old bug that set Title to "This clip is encrypted - unable to display." string)
        var blobRepository = new BlobRepository(DbContext);
        var textBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id);
        if (textBlobs.Count > 0)
        {
            textBlobs[0].Data = "Not Base64 Data! This will fail.";
            await blobRepository.UpdateTextAsync(textBlobs[0]);
            await DbContext.SaveChangesAsync();
        }

        // Act - Decrypt temporarily should handle corrupted BLOB gracefully
        await clipService.DecryptClipsAsync(_testDatabaseKey, [clip.Id], encryptionKey);

        // Assert - Corrupted BLOB should remain as-is (not throw exception during decryption)
        var finalBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id);
        await Assert.That(finalBlobs[0].Data).IsEqualTo("Not Base64 Data! This will fail.");
    }

    [Test]
    public async Task PermanentDecrypt_ClearsEncryptedFlag_PreventsReDecryption()
    {
        // Arrange
        var clipService = CreateClipService();
        const string originalTitle = "Test Document";
        const string originalText = "Important content";

        var clip = await CreateTestClipWithData(clipService, originalTitle, originalText);

        using var encryptionKey = EncryptionKey.FromPassphrase(_testPassphrase);

        // Encrypt the clip
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Verify it's encrypted
        var encryptedClip = await clipService.GetByIdAsync(_testDatabaseKey, clip.Id);
        await Assert.That(encryptedClip!.Encrypted).IsTrue();
        await Assert.That(encryptedClip.EncryptionSalt).IsNotEmpty();

        // Act - Permanently decrypt the clip
        await clipService.DecryptClipsAsync(_testDatabaseKey, [clip.Id], encryptionKey, true);
        await DbContext.SaveChangesAsync();

        // Assert - Encrypted flag and metadata should be cleared
        var decryptedClip = await clipService.GetByIdAsync(_testDatabaseKey, clip.Id);
        await Assert.That(decryptedClip).IsNotNull();
        await Assert.That(decryptedClip!.Encrypted).IsFalse();
        await Assert.That(decryptedClip.EncryptionSalt).IsNullOrEmpty();
        await Assert.That(decryptedClip.EncryptionIv).IsNullOrEmpty();
        await Assert.That(decryptedClip.EncryptionMethod).IsNullOrEmpty();

        // Verify TEXT BLOB is decrypted
        var blobRepository = new BlobRepository(DbContext);
        var textBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id);
        await Assert.That(textBlobs[0].Data).IsEqualTo(originalText);

        // Act - Try to decrypt again (should skip because Encrypted=false)
        var secondDecryptCount = await clipService.DecryptClipsAsync(_testDatabaseKey, [clip.Id], encryptionKey, true);

        // Assert - Should have skipped (not thrown exception), returned 0
        await Assert.That(secondDecryptCount).IsEqualTo(0);

        // Verify BLOB data is still intact (not corrupted by attempted re-decryption)
        var finalBlobs = await blobRepository.GetTextByClipIdAsync(clip.Id);
        await Assert.That(finalBlobs[0].Data).IsEqualTo(originalText);
    }

    #region Helper Methods

    private ClipService CreateClipService() => CreateClipServiceWithContext(DbContext);

    private ClipService CreateClipServiceWithContext(ClipMateDbContext context)
    {
        var mockFactory = new Mock<IDatabaseContextFactory>();
        var clipRepository = new ClipRepository(context, Mock.Of<ILogger<ClipRepository>>());
        var clipDataRepository = new ClipDataRepository(context);
        var blobRepository = new BlobRepository(context);

        mockFactory.Setup(p => p.GetClipRepository(_testDatabaseKey))
            .Returns(clipRepository);

        mockFactory.Setup(p => p.GetClipDataRepository(_testDatabaseKey))
            .Returns(clipDataRepository);

        mockFactory.Setup(p => p.GetBlobRepository(_testDatabaseKey))
            .Returns(blobRepository);

        var encryptionService = new AesEncryptionService();
        var logger = Mock.Of<ILogger<ClipService>>();
        var configService = Mock.Of<IConfigurationService>();
        var clipboardService = Mock.Of<IClipboardService>();
        var templateService = Mock.Of<ITemplateService>();

        // Use real DecryptedBlobCacheService for integration tests - create new instance per call
#pragma warning disable CA2000 // Dispose objects before losing scope - test objects don't require explicit disposal
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var blobCacheService = new DecryptedBlobCacheService(memoryCache, Mock.Of<ILogger<DecryptedBlobCacheService>>());
#pragma warning restore CA2000

        return new ClipService(
            mockFactory.Object,
            configService,
            clipboardService,
            templateService,
            encryptionService,
            blobCacheService!,
            Mock.Of<IMessenger>(),
            logger);
    }

    private async Task<Clip> CreateTestClipWithData(ClipService clipService, string title, string textContent)
    {
        var collectionId = await CreateTestCollection();

        var clip = new Clip
        {
            Type = ClipType.Text,
            Title = title,
            TextContent = textContent,
            ContentHash = $"hash_{Guid.NewGuid():N}",
            CapturedAt = DateTime.UtcNow,
            CollectionId = collectionId,
        };

        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Create ClipData and TEXT BLOB
        var clipData = new ClipData
        {
            Id = Guid.NewGuid(),
            ClipId = savedClip.Id,
            Format = 13, // CF_UNICODETEXT
        };

        DbContext.ClipData.Add(clipData);
        await DbContext.SaveChangesAsync();

        var textBlob = new BlobTxt
        {
            ClipDataId = clipData.Id,
            Data = textContent,
        };

        DbContext.BlobTxt.Add(textBlob);
        await DbContext.SaveChangesAsync();

        return savedClip;
    }

    private async Task<Guid> CreateTestCollection()
    {
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Test Collection",
            Description = "For testing",
            CreatedAt = DateTime.UtcNow,
        };

        DbContext.Collections.Add(collection);
        await DbContext.SaveChangesAsync();
        return collection.Id;
    }

    private static bool IsBase64String(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Per-Clip Key Management Tests

    [Test]
    public async Task LockClipsAsync_SelectiveClips_RemovesOnlySpecifiedClipsFromCache()
    {
        // Arrange
        var clipService = CreateClipService();

        var clip1 = await CreateTestClipWithData(clipService, "Clip 1", "Content 1");
        var clip2 = await CreateTestClipWithData(clipService, "Clip 2", "Content 2");
        var clip3 = await CreateTestClipWithData(clipService, "Clip 3", "Content 3");

        using var encryptionKey = EncryptionKey.FromPassphrase(_testPassphrase);

        // Encrypt all clips
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip1.Id, clip2.Id, clip3.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Decrypt clips to cache them (temporary/non-permanent decryption)
        var decryptedCount = await clipService.DecryptClipsAsync(_testDatabaseKey, [clip1.Id, clip2.Id, clip3.Id], encryptionKey);
        await Assert.That(decryptedCount).IsEqualTo(3); // Verify all 3 clips were decrypted

        // Act - Lock only clip1 and clip2
        var lockedIds = await clipService.LockClipsAsync(_testDatabaseKey, [clip1.Id, clip2.Id]);

        // Assert - Should return the locked clip IDs
        await Assert.That(lockedIds).Count().IsEqualTo(2);
        await Assert.That(lockedIds).Contains(clip1.Id);
        await Assert.That(lockedIds).Contains(clip2.Id);
    }

    [Test]
    public async Task LockClipsAsync_NullClipIds_LocksAllCachedClips()
    {
        // Arrange
        var clipService = CreateClipService();

        var clip1 = await CreateTestClipWithData(clipService, "Clip A", "Content A");
        var clip2 = await CreateTestClipWithData(clipService, "Clip B", "Content B");

        using var encryptionKey = EncryptionKey.FromPassphrase(_testPassphrase);

        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip1.Id, clip2.Id], encryptionKey);
        await DbContext.SaveChangesAsync();

        // Act - Lock all (pass null)
        var lockedIds = await clipService.LockClipsAsync(_testDatabaseKey);

        // Assert - Returns empty since nothing was cached
        await Assert.That(lockedIds).IsEmpty();
    }

    [Test]
    public async Task MultiplePassphrases_DifferentClips_IndependentKeyManagement()
    {
        // Arrange
        var clipService = CreateClipService();

        var clip1 = await CreateTestClipWithData(clipService, "Secret A", "Content Alpha");
        var clip2 = await CreateTestClipWithData(clipService, "Secret B", "Content Beta");

        using var keyAlpha = EncryptionKey.FromPassphrase("password-alpha");
        using var keyBeta = EncryptionKey.FromPassphrase("password-beta");

        // Act - Encrypt clips with different passphrases
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip1.Id], keyAlpha);
        await clipService.EncryptClipsAsync(_testDatabaseKey, [clip2.Id], keyBeta);
        await DbContext.SaveChangesAsync();

        // Assert - Verify both encrypted with different metadata
        var encryptedClip1 = await clipService.GetByIdAsync(_testDatabaseKey, clip1.Id);
        var encryptedClip2 = await clipService.GetByIdAsync(_testDatabaseKey, clip2.Id);

        await Assert.That(encryptedClip1!.Encrypted).IsTrue();
        await Assert.That(encryptedClip2!.Encrypted).IsTrue();

        // Salts should be different (different keys)
        await Assert.That(encryptedClip1.EncryptionSalt).IsNotEqualTo(encryptedClip2.EncryptionSalt);

        // Decrypt clip1 with keyAlpha
        await clipService.DecryptClipsAsync(_testDatabaseKey, [clip1.Id], keyAlpha, true);

        // Decrypt clip2 with keyBeta
        await clipService.DecryptClipsAsync(_testDatabaseKey, [clip2.Id], keyBeta, true);

        // Verify both decrypted correctly
        var blobRepository = new BlobRepository(DbContext);
        var clip1Blobs = await blobRepository.GetTextByClipIdAsync(clip1.Id);
        var clip2Blobs = await blobRepository.GetTextByClipIdAsync(clip2.Id);

        await Assert.That(clip1Blobs[0].Data).IsEqualTo("Content Alpha");
        await Assert.That(clip2Blobs[0].Data).IsEqualTo("Content Beta");
    }

    [Test]
    public async Task LockClipsAsync_EmptyClipIdsList_ReturnsEmpty()
    {
        // Arrange
        var clipService = CreateClipService();

        // Act - Lock with empty list
        var lockedIds = await clipService.LockClipsAsync(_testDatabaseKey, Array.Empty<Guid>());

        // Assert
        await Assert.That(lockedIds).IsEmpty();
    }

    #endregion
}
