using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform.Helpers;
using ClipMate.Tests.Integration.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for changing a clip's stored text (Edit > Case Conversion).
/// </summary>
public class ClipTransformTextTests : IntegrationTestBase
{
    private const string _testDatabaseKey = "db_test0001";

    [Test]
    public async Task TransformTextAsync_ConvertsTextAndTitle_AndUpdatesHashAndSize()
    {
        // Arrange
        var clipId = await SeedClipAsync("Some Text", "Some Title", false);
        var clipService = CreateClipService();

        // Act
        var converted = await clipService.TransformTextAsync(_testDatabaseKey, clipId, p => p.ToUpperInvariant(), p => p.ToUpperInvariant());

        // Assert
        await Assert.That(converted).IsNotNull();
        await Assert.That(converted!.Title).IsEqualTo("SOME TITLE");
        await Assert.That(converted.ContentHash).IsEqualTo(ContentHasher.HashText("SOME TEXT"));
        await Assert.That(converted.Checksum).IsEqualTo(ContentHasher.Checksum(converted.ContentHash));
        await Assert.That(converted.Size).IsEqualTo("SOME TEXT".Length * 2);
        await Assert.That(await DbContext.BlobTxt.AsNoTracking().Where(p => p.ClipId == clipId && p.Data == "SOME TEXT").CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task TransformTextAsync_RemovesTheOtherFormats_WhichStillHoldTheOldText()
    {
        // Arrange: text and HTML
        var clipId = await SeedClipAsync("Some Text", "Some Title", false);
        var html = new ClipData { Id = Guid.NewGuid(), ClipId = clipId, Format = Formats.Html.Code, FormatName = Formats.Html.Name, StorageType = StorageType.Text, Size = 18 };
        DbContext.ClipData.Add(html);
        DbContext.BlobTxt.Add(new BlobTxt { Id = Guid.NewGuid(), ClipDataId = html.Id, ClipId = clipId, Data = "<b>Some Text</b>" });
        await DbContext.SaveChangesAsync();
        var clipService = CreateClipService();

        // Act
        var converted = await clipService.TransformTextAsync(_testDatabaseKey, clipId, p => p.ToUpperInvariant(), p => p.ToUpperInvariant());

        // Assert: only the text is left
        await Assert.That(converted!.HasHtml).IsFalse();
        await Assert.That(await DbContext.ClipData.AsNoTracking().Where(p => p.ClipId == clipId).CountAsync()).IsEqualTo(1);
        await Assert.That(await DbContext.BlobTxt.AsNoTracking().Where(p => p.ClipId == clipId && p.Data.Contains("<b>")).CountAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task TransformTextAsync_LeavesAnEncryptedClipAlone()
    {
        // Arrange
        var clipId = await SeedClipAsync("ciphertext", "Encrypted", true);
        var clipService = CreateClipService();

        // Act
        var converted = await clipService.TransformTextAsync(_testDatabaseKey, clipId, p => p.ToUpperInvariant(), p => p.ToUpperInvariant());

        // Assert
        await Assert.That(converted).IsNull();
        await Assert.That(await DbContext.BlobTxt.AsNoTracking().Where(p => p.ClipId == clipId && p.Data == "ciphertext").CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task TransformTextAsync_LeavesAClipWithoutTextAlone()
    {
        // Arrange: a clip with no text format
        var clip = new Clip { Id = Guid.NewGuid(), Title = "A Picture", Type = ClipType.Image, ContentHash = "image_hash", CapturedAt = DateTime.UtcNow };
        DbContext.Clips.Add(clip);
        DbContext.ClipData.Add(new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.Bitmap.Code, FormatName = Formats.Bitmap.Name, StorageType = StorageType.Binary, Size = 10 });
        await DbContext.SaveChangesAsync();
        var clipService = CreateClipService();

        // Act
        var converted = await clipService.TransformTextAsync(_testDatabaseKey, clip.Id, p => p.ToUpperInvariant(), p => p.ToUpperInvariant());

        // Assert
        await Assert.That(converted).IsNull();
        await Assert.That(await DbContext.ClipData.AsNoTracking().Where(p => p.ClipId == clip.Id).CountAsync()).IsEqualTo(1);
    }

    private async Task<Guid> SeedClipAsync(string text, string title, bool encrypted)
    {
        var clip = new Clip { Id = Guid.NewGuid(), Title = title, Type = ClipType.Text, ContentHash = "old_hash", Encrypted = encrypted, CapturedAt = DateTime.UtcNow };
        var clipData = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.UnicodeText.Code, FormatName = Formats.UnicodeText.Name, StorageType = StorageType.Text, Size = text.Length * 2 };
        DbContext.Clips.Add(clip);
        DbContext.ClipData.Add(clipData);
        DbContext.BlobTxt.Add(new BlobTxt { Id = Guid.NewGuid(), ClipDataId = clipData.Id, ClipId = clip.Id, Data = text });
        await DbContext.SaveChangesAsync();
        return clip.Id;
    }

    private IClipService CreateClipService()
    {
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(_testDatabaseKey))
            .Returns(new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>()));

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
