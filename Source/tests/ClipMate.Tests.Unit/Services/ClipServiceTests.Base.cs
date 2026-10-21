using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

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
    protected IDecryptedBlobCacheService MockBlobCacheService = null!;
    protected IBlobRepository MockBlobRepository = null!;
    protected IClipDataRepository MockClipDataRepository = null!;
    protected IClipRepository MockClipRepository = null!;
    protected IConfigurationService MockConfigService = null!;
    protected IDatabaseContextFactory MockContextFactory = null!;
    protected IEncryptionService MockEncryptionService = null!;
    protected ILogger<ClipService> MockLogger = null!;
    protected IClipboardService MockClipboardService = null!;
    protected IRemoteDatabaseService MockRemoteDatabases = null!;
    protected ITemplateService MockTemplateService = null!;

    [Before(Test)]
    public void Setup()
    {
        MockClipRepository = A.Fake<IClipRepository>();
        MockClipDataRepository = A.Fake<IClipDataRepository>();
        MockBlobRepository = A.Fake<IBlobRepository>();
        MockEncryptionService = A.Fake<IEncryptionService>();
        MockBlobCacheService = A.Fake<IDecryptedBlobCacheService>();
        MockConfigService = A.Fake<IConfigurationService>();
        MockContextFactory = A.Fake<IDatabaseContextFactory>();
        MockLogger = A.Fake<ILogger<ClipService>>();
        MockClipboardService = A.Fake<IClipboardService>();
        MockRemoteDatabases = A.Fake<IRemoteDatabaseService>();
        MockTemplateService = A.Fake<ITemplateService>();
        A.CallTo(() => MockTemplateService.TryApplyTemplate(A<Clip>._)).Returns(null);

        // Setup factory to return our mock repositories
        A.CallTo(() => MockContextFactory.GetClipRepository(A<string>.Ignored))
            .Returns(MockClipRepository);

        A.CallTo(() => MockContextFactory.GetClipDataRepository(A<string>.Ignored))
            .Returns(MockClipDataRepository);

        A.CallTo(() => MockContextFactory.GetBlobRepository(A<string>.Ignored))
            .Returns(MockBlobRepository);

        // Default mock for UpdateAsync - returns Task.CompletedTask
        A.CallTo(() => MockClipRepository.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored))
            .Returns(true);
    }

    protected IClipService CreateClipService() =>
        new ClipService(
            MockContextFactory,
            MockConfigService,
            MockClipboardService,
            MockTemplateService,
            MockEncryptionService,
            MockBlobCacheService,
            new ClipContentService(MockContextFactory, MockRemoteDatabases),
            A.Fake<IMessenger>(),
            MockLogger);

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
