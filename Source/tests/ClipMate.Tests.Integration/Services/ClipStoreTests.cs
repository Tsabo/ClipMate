using ClipMate.Core.Models;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// The clip store against a real SQLite Database: one hash and checksum for every clip, and GLOSSARY's Duplicate rule
/// for captures that ask for it.
/// </summary>
public class ClipStoreTests : IntegrationTestBase
{
    private const string _databaseKey = "db_store";

    private Collection _collection = null!;
    private ClipRepository _clipRepository = null!;
    private ClipStore _store = null!;

    [Before(Test)]
    public async Task SetupStoreAsync()
    {
        _collection = new Collection { Id = Guid.NewGuid(), Name = "Inbox", CreatedAt = DateTime.UtcNow, AcceptNewClips = true };
        DbContext.Collections.Add(_collection);
        await DbContext.SaveChangesAsync();

        _clipRepository = new ClipRepository(DbContext, A.Fake<ILogger<ClipRepository>>());
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(_databaseKey)).Returns(_clipRepository);
        A.CallTo(() => contextFactory.GetCollectionRepository(_databaseKey)).Returns(new CollectionRepository(DbContext));
        _store = new ClipStore(contextFactory, A.Fake<ILogger<ClipStore>>());
    }

    [Test]
    public async Task AddAsync_NewClip_GetsTheContentHashAndAStableChecksum()
    {
        // Arrange: a made-up hash is replaced by the real one
        var clip = TextClip("Hello", contentHash: "made-up");

        // Act
        var added = (ClipAdded)await _store.AddAsync(_databaseKey, clip, _collection.Id, null, false);

        // Assert
        var stored = await _clipRepository.GetByIdAsync(added.Clip.Id);
        await Assert.That(stored!.ContentHash).IsEqualTo(ContentHasher.HashText("Hello"));
        await Assert.That(stored.Checksum).IsEqualTo(ContentHasher.Checksum(ContentHasher.HashText("Hello")));
        await Assert.That(stored.Checksum).IsNotEqualTo(0);
        await Assert.That(stored.CollectionId).IsEqualTo(_collection.Id);
    }

    [Test]
    public async Task AddAsync_CopyWithoutLoadedContent_KeepsTheHashItWasGiven()
    {
        // Arrange: Copy and Move build the new row from the stored one, then copy its Formats
        var copy = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, Title = "Copy", ContentHash = "ABCDEF0123", CapturedAt = DateTimeOffset.Now };

        // Act
        var added = (ClipAdded)await _store.AddAsync(_databaseKey, copy, _collection.Id, null, false);

        // Assert
        await Assert.That(added.Clip.ContentHash).IsEqualTo("ABCDEF0123");
    }

    [Test]
    public async Task AddAsync_SameTextAsTheNewestClip_IsADuplicateWhenAsked()
    {
        // Arrange: Inbox doesn't accept duplicates by default
        var first = (ClipAdded)await _store.AddAsync(_databaseKey, TextClip("Hello"), _collection.Id, null, true);

        // Act
        var again = await _store.AddAsync(_databaseKey, TextClip("Hello"), _collection.Id, null, true);

        // Assert
        await Assert.That(again).IsTypeOf<DuplicateFound>();
        await Assert.That(((DuplicateFound)again).Existing.Id).IsEqualTo(first.Clip.Id);
    }

    [Test]
    public async Task AddAsync_SameTextNotAskingForTheRule_IsAdded()
    {
        // Arrange: New Clip, import and Append never check for Duplicates
        await _store.AddAsync(_databaseKey, TextClip("Hello"), _collection.Id, null, false);

        // Act
        var again = await _store.AddAsync(_databaseKey, TextClip("Hello"), _collection.Id, null, false);

        // Assert
        await Assert.That(again).IsTypeOf<ClipAdded>();
    }

    [Test]
    public async Task AddAsync_CollectionThatAcceptsDuplicates_IsAdded()
    {
        // Arrange
        _collection.AcceptDuplicates = true;
        await DbContext.SaveChangesAsync();
        await _store.AddAsync(_databaseKey, TextClip("Hello"), _collection.Id, null, true);

        // Act
        var again = await _store.AddAsync(_databaseKey, TextClip("Hello"), _collection.Id, null, true);

        // Assert
        await Assert.That(again).IsTypeOf<ClipAdded>();
    }

    [Test]
    public async Task AddAsync_NoPlainText_IsNeverADuplicate()
    {
        // Arrange: two copies of the same image
        await _store.AddAsync(_databaseKey, ImageClip(), _collection.Id, null, true);

        // Act
        var again = await _store.AddAsync(_databaseKey, ImageClip(), _collection.Id, null, true);

        // Assert
        await Assert.That(again).IsTypeOf<ClipAdded>();
    }

    private static Clip TextClip(string text, string contentHash = "") => new()
    {
        Id = Guid.NewGuid(),
        Type = ClipType.Text,
        Title = text,
        TextContent = text,
        ContentHash = contentHash,
        CapturedAt = DateTimeOffset.Now,
    };

    private static Clip ImageClip() => new()
    {
        Id = Guid.NewGuid(),
        Type = ClipType.Image,
        Title = "Image",
        ImageData = [1, 2, 3, 4],
        CapturedAt = DateTimeOffset.Now,
    };
}
