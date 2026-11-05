using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
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
/// Integration tests for the commands that change a clip's stored text: Edit > Clean-Up Text, Remove Linebreaks and
/// Strip Out Non-TEXT Data Formats.
/// </summary>
public class ClipTextCommandsTests : IntegrationTestBase
{
    private const string _testDatabaseKey = "db_test0001";
    private readonly TextTransformService _transforms = new();

    [Test]
    public async Task RemoveLinebreaks_SavesTheJoinedText_AndKeepsTheTitle()
    {
        // Arrange
        var clipId = await SeedClipAsync("one\r\ntwo\r\n\r\nthree", "My Title");

        // Act
        var changed = await CreateClipService().TransformTextAsync(_testDatabaseKey, clipId, p => _transforms.RemoveLineBreaks(p, LineBreakMode.PreserveParagraphs), null);

        // Assert
        const string expected = "one two\r\n\r\nthree";
        await Assert.That(changed!.Title).IsEqualTo("My Title");
        await Assert.That(changed.ContentHash).IsEqualTo(ContentHasher.HashText(expected));
        await Assert.That(changed.Size).IsEqualTo(expected.Length * 2);
        await Assert.That(await SavedTextAsync(clipId)).IsEqualTo(expected);
    }

    [Test]
    public async Task StripNonText_RemovesTheOtherFormats_AndKeepsTheTextAndTitle()
    {
        // Arrange: text, HTML and a picture
        var clipId = await SeedClipAsync("Some Text", "My Title");
        var html = new ClipData { Id = Guid.NewGuid(), ClipId = clipId, Format = Formats.Html.Code, FormatName = Formats.Html.Name, StorageType = StorageType.Text, Size = 18 };
        DbContext.ClipData.Add(html);
        DbContext.BlobTxt.Add(new BlobTxt { Id = Guid.NewGuid(), ClipDataId = html.Id, ClipId = clipId, Data = "<b>Some Text</b>" });
        DbContext.ClipData.Add(new ClipData { Id = Guid.NewGuid(), ClipId = clipId, Format = Formats.Bitmap.Code, FormatName = Formats.Bitmap.Name, StorageType = StorageType.Binary, Size = 10 });
        await DbContext.SaveChangesAsync();

        // Act
        var changed = await CreateClipService().TransformTextAsync(_testDatabaseKey, clipId, p => p, null);

        // Assert
        await Assert.That(changed!.Title).IsEqualTo("My Title");
        await Assert.That(changed.HasHtml).IsFalse();
        await Assert.That(changed.HasBitmap).IsFalse();
        await Assert.That(changed.Type).IsEqualTo(ClipType.Text);
        await Assert.That(await DbContext.ClipData.AsNoTracking().Where(p => p.ClipId == clipId).CountAsync()).IsEqualTo(1);
        await Assert.That(await SavedTextAsync(clipId)).IsEqualTo("Some Text");
    }

    [Test]
    public async Task StripNonText_LeavesAClipWithoutTextAlone()
    {
        // Arrange: a picture
        var clip = new Clip { Id = Guid.NewGuid(), Title = "A Picture", Type = ClipType.Image, ContentHash = "image_hash", CapturedAt = DateTime.UtcNow };
        DbContext.Clips.Add(clip);
        DbContext.ClipData.Add(new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.Bitmap.Code, FormatName = Formats.Bitmap.Name, StorageType = StorageType.Binary, Size = 10 });
        await DbContext.SaveChangesAsync();

        // Act
        var changed = await CreateClipService().TransformTextAsync(_testDatabaseKey, clip.Id, p => p, null);

        // Assert
        await Assert.That(changed).IsNull();
        await Assert.That(await DbContext.ClipData.AsNoTracking().Where(p => p.ClipId == clip.Id).CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task CleanUpText_SavesTheCleanedText_AndChangesTheTitle()
    {
        // Arrange
        var clipId = await SeedClipAsync("> quoted\r\n> text", "> my title");
        var options = new TextCleanupConfiguration { LineBreaks = TextCleanupLineBreaks.All, Case = TextCleanupCase.Upper };

        // Act
        var changed = await CreateClipService().TransformTextAsync(_testDatabaseKey, clipId, p => _transforms.CleanUpText(p, options), p => _transforms.CleanUpTitle(p, options));

        // Assert: the title gets the case change only
        await Assert.That(changed!.Title).IsEqualTo("> MY TITLE");
        await Assert.That(await SavedTextAsync(clipId)).IsEqualTo("QUOTED TEXT");
    }

    [Test]
    public async Task TheCommands_LeaveAnEncryptedClipAlone()
    {
        // Arrange
        var clipId = await SeedClipAsync("cipher\r\ntext", "Encrypted", true);

        // Act
        var changed = await CreateClipService().TransformTextAsync(_testDatabaseKey, clipId, p => _transforms.RemoveLineBreaks(p, LineBreakMode.RemoveAll), null);

        // Assert
        await Assert.That(changed).IsNull();
        await Assert.That(await SavedTextAsync(clipId)).IsEqualTo("cipher\r\ntext");
    }

    private Task<string> SavedTextAsync(Guid clipId) =>
        DbContext.BlobTxt.AsNoTracking().Where(p => p.ClipId == clipId).Select(p => p.Data).SingleAsync();

    private async Task<Guid> SeedClipAsync(string text, string title, bool encrypted = false)
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
