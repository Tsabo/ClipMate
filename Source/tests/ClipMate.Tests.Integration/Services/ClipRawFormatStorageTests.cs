using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Tests.Integration.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Storing the Formats a clip keeps raw: an application's private formats and the others no typed field holds.
/// </summary>
public class ClipRawFormatStorageTests : IntegrationTestBase
{
    private const string _testDatabaseKey = "db_test0001";

    [Test]
    public async Task CreateAsync_ClipWithRawFormats_StoresEachInTheSourcesOrderAndLoadsThemBack()
    {
        // Arrange: text copied with an application's private format and CF_LOCALE
        var clipService = CreateClipService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = "hello",
            ContentHash = "raw_formats_hash_1",
            CapturedAt = DateTime.UtcNow,
            RawFormats =
            [
                new RawClipboardFormat("Acme Private", 0xC123, [1, 2, 3]),
                new RawClipboardFormat("CF_LOCALE", (uint)Formats.Locale.Code, [9, 4, 0, 0]),
            ],
        };

        // Act
        var savedClip = await clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        var formats = DbContext.ClipData.Where(p => p.ClipId == savedClip.Id).ToList();
        var binaryBlobs = DbContext.BlobBlob.Where(p => p.ClipId == savedClip.Id).ToList();
        var textBlobs = DbContext.BlobTxt.Where(p => p.ClipId == savedClip.Id).ToList();
        var loaded = new Clip { Id = savedClip.Id, Type = ClipType.Text };
        new ClipContent(savedClip, formats, textBlobs, [], [], binaryBlobs).PopulateContent(loaded);

        // Assert
        var privateFormat = formats.Single(p => p.FormatName == "Acme Private");
        await Assert.That(privateFormat.Format).IsEqualTo(0xC123);
        await Assert.That(privateFormat.StorageType).IsEqualTo(StorageType.Binary);
        await Assert.That(privateFormat.Size).IsEqualTo(3);
        await Assert.That(loaded.TextContent).IsEqualTo("hello");
        await Assert.That(loaded.RawFormats.Select(p => p.Name)).IsEquivalentTo(["Acme Private", "CF_LOCALE"]);
        await Assert.That(loaded.RawFormats.Single(p => p.Name == "CF_LOCALE").Data).IsEquivalentTo(new byte[] { 9, 4, 0, 0 });
    }

    private IClipService CreateClipService()
    {
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var repository = new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>());
        A.CallTo(() => contextFactory.GetClipRepository(_testDatabaseKey)).Returns(repository);

        return new ClipService(
            contextFactory,
            new ClipStore(contextFactory, NullLogger<ClipStore>.Instance),
            A.Fake<IConfigurationService>(),
            A.Fake<IEncryptionService>(),
            A.Fake<IDecryptedBlobCacheService>(),
            new RepositoryDatabases(contextFactory),
            A.Fake<IMessenger>(),
            A.Fake<ILogger<ClipService>>());
    }
}
