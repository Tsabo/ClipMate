using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base test class for ClipService tests.
/// Contains shared setup, mocks, and helper methods.
/// </summary>
[Category("ClipService")]
[Category("Unit")]
public partial class ClipServiceTests
{
    protected const string TestDatabaseKey = "db_test0001";
    protected Mock<IDecryptedBlobCacheService> MockBlobCacheService = null!;
    protected Mock<IBlobRepository> MockBlobRepository = null!;
    protected Mock<IClipDataRepository> MockClipDataRepository = null!;
    protected Mock<IClipRepository> MockClipRepository = null!;
    protected Mock<IConfigurationService> MockConfigService = null!;
    protected Mock<IDatabaseContextFactory> MockContextFactory = null!;
    protected Mock<IEncryptionService> MockEncryptionService = null!;
    protected Mock<ILogger<ClipService>> MockLogger = null!;

    [Before(Test)]
    public void Setup()
    {
        MockClipRepository = new Mock<IClipRepository>();
        MockClipDataRepository = new Mock<IClipDataRepository>();
        MockBlobRepository = new Mock<IBlobRepository>();
        MockEncryptionService = new Mock<IEncryptionService>();
        MockBlobCacheService = new Mock<IDecryptedBlobCacheService>();
        MockConfigService = new Mock<IConfigurationService>();
        MockContextFactory = new Mock<IDatabaseContextFactory>();
        MockLogger = new Mock<ILogger<ClipService>>();

        // Setup factory to return our mock repositories
        MockContextFactory.Setup(p => p.GetClipRepository(It.IsAny<string>()))
            .Returns(MockClipRepository.Object);

        MockContextFactory.Setup(p => p.GetClipDataRepository(It.IsAny<string>()))
            .Returns(MockClipDataRepository.Object);

        MockContextFactory.Setup(p => p.GetBlobRepository(It.IsAny<string>()))
            .Returns(MockBlobRepository.Object);

        // Default mock for UpdateAsync - returns Task.CompletedTask
        MockClipRepository.Setup(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    protected IClipService CreateClipService() =>
        new ClipService(
            MockContextFactory.Object,
            MockConfigService.Object,
            Mock.Of<IClipboardService>(),
            Mock.Of<ITemplateService>(),
            MockEncryptionService.Object,
            MockBlobCacheService.Object,
            Mock.Of<IMessenger>(),
            MockLogger.Object);

    protected static Clip CreateTestClip(Guid? id = null,
        DateTime? capturedAt = null,
        Guid? collectionId = null,
        string contentHash = "TEST_HASH",
        bool isFavorite = false,
        bool encrypted = false,
        string? encryptionSalt = null,
        string? encryptionIv = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test content",
            ContentHash = contentHash,
            CapturedAt = capturedAt ?? DateTime.UtcNow,
            CollectionId = collectionId ?? Guid.NewGuid(),
            IsFavorite = isFavorite,
            Encrypted = encrypted,
            EncryptionSalt = encryptionSalt,
            EncryptionIv = encryptionIv,
            EncryptionMethod = encrypted
                ? "AES-256"
                : null,
        };

    protected static ClipData CreateTestClipData(Guid clipId, int format = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClipId = clipId,
            Format = format,
            FormatName = "CF_TEXT",
            Size = 100,
            StorageType = 1,
        };

    protected static BlobTxt CreateTestTextBlob(Guid clipId, string data = "Test text") =>
        new()
        {
            Id = Guid.NewGuid(),
            ClipId = clipId,
            ClipDataId = Guid.NewGuid(),
            Data = data,
        };

    protected static BlobJpg CreateTestJpgBlob(Guid clipId, byte[]? data = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClipId = clipId,
            ClipDataId = Guid.NewGuid(),
            Data = data ?? [1, 2, 3],
        };

    protected static BlobPng CreateTestPngBlob(Guid clipId, byte[]? data = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClipId = clipId,
            ClipDataId = Guid.NewGuid(),
            Data = data ?? [4, 5, 6],
        };

    protected static BlobBlob CreateTestBinaryBlob(Guid clipId, byte[]? data = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClipId = clipId,
            ClipDataId = Guid.NewGuid(),
            Data = data ?? [7, 8, 9],
        };
}
