using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Copy to local end to end: a clip in a Peer's Shared Database (its own SQLite file, read through the Peer's
/// <see cref="ClipContentService" /> exactly as the Peer serves it) is copied into a local Database.
/// </summary>
public class CopyToLocalIntegrationTests : IntegrationTestBase
{
    private const string _peerDatabaseKey = "db_peer0001";
    private const string _localDatabaseKey = "db_local001";
    private const string _passphrase = "Copy-To-Local-Passphrase!";

    private static readonly string _remoteDatabaseKey =
        RemoteDatabaseKey.From(new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([7, 7, 7]), Guid.NewGuid()));

    private ClipMateDbContext _peerContext = null!;
    private ClipService _peerClipService = null!;
    private ClipService _localClipService = null!;
    private Guid _peerCollectionId;
    private Guid _localCollectionId;

    [Before(Test)]
    public async Task SetupDatabasesAsync()
    {
        _peerContext = new ClipMateDbContext(new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options);

        await _peerContext.Database.OpenConnectionAsync();
        await _peerContext.Database.EnsureCreatedAsync();

        var factory = A.Fake<IDatabaseContextFactory>();
        Map(factory, _peerDatabaseKey, _peerContext);
        Map(factory, _localDatabaseKey, DbContext);

        // The Peer reads its own Database; the requester reaches it through IRemoteDatabaseService
        var peerContentService = new ClipContentService(factory, A.Fake<IRemoteDatabaseService>());
        var remoteDatabases = A.Fake<IRemoteDatabaseService>();
        A.CallTo(() => remoteDatabases.GetClipContentAsync(A<RemoteDatabaseAddress>._, A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily((RemoteDatabaseAddress _, Guid clipId, CancellationToken token) =>
                peerContentService.GetClipContentAsync(_peerDatabaseKey, clipId, token));

        _peerClipService = CreateClipService(factory, peerContentService);
        _localClipService = CreateClipService(factory, new ClipContentService(factory, remoteDatabases));

        _peerCollectionId = await AddCollectionAsync(_peerContext, "Peer collection");
        _localCollectionId = await AddCollectionAsync(DbContext, "Local collection");
    }

    [After(Test)]
    public async Task CleanupDatabasesAsync() => await _peerContext.DisposeAsync();

    [Test]
    public async Task CopyToLocalAsync_RemoteClip_CreatesLocalClipWithAllFormatsAndMetadata()
    {
        // Arrange
        var capturedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var source = await _peerClipService.CreateAsync(_peerDatabaseKey, new Clip
        {
            Type = ClipType.Html,
            Title = "Release notes",
            CustomTitle = true,
            Creator = "jeremy@peer",
            SourceUrl = "https://example.com/notes",
            SourceApplicationName = "msedge.exe",
            SourceApplicationTitle = "Notes - Edge",
            CapturedAt = capturedAt,
            ContentHash = "hash-release-notes",
            Label = "work",
            IsFavorite = true,
            Locale = 1033,
            Macro = true,
            TextContent = "Plain text",
            HtmlContent = "<b>Plain text</b>",
            RtfContent = @"{\rtf1 Plain text}",
            CollectionId = _peerCollectionId,
        });

        // Act
        var copy = await _localClipService.CopyToLocalAsync(_remoteDatabaseKey, source.Id, _localDatabaseKey, _localCollectionId);

        // Assert
        var stored = await _localClipService.GetByIdAsync(_localDatabaseKey, copy.Id);
        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.Id).IsNotEqualTo(source.Id);
        await Assert.That(stored.CollectionId).IsEqualTo(_localCollectionId);
        await Assert.That(stored.Title).IsEqualTo("Release notes");
        await Assert.That(stored.CustomTitle).IsTrue();
        await Assert.That(stored.Creator).IsEqualTo("jeremy@peer");
        await Assert.That(stored.SourceUrl).IsEqualTo("https://example.com/notes");
        await Assert.That(stored.SourceApplicationName).IsEqualTo("msedge.exe");
        await Assert.That(stored.SourceApplicationTitle).IsEqualTo("Notes - Edge");
        await Assert.That(stored.CapturedAt).IsEqualTo(capturedAt);
        await Assert.That(stored.ContentHash).IsEqualTo("hash-release-notes");
        await Assert.That(stored.Type).IsEqualTo(ClipType.Html);
        await Assert.That(stored.Label).IsEqualTo("work");
        await Assert.That(stored.IsFavorite).IsTrue();
        await Assert.That(stored.Locale).IsEqualTo(1033);
        await Assert.That(stored.Macro).IsTrue();

        var localContent = await new ClipContentService(CreateLocalOnlyFactory(), A.Fake<IRemoteDatabaseService>())
            .GetClipContentAsync(_localDatabaseKey, copy.Id);

        await Assert.That(localContent).IsNotNull();
        await Assert.That(localContent!.Formats.Count).IsEqualTo(3);
        await Assert.That(localContent.TextBlobs.Select(p => p.Data).Order().ToList())
            .IsEquivalentTo(new List<string> { "<b>Plain text</b>", "Plain text", @"{\rtf1 Plain text}" });
    }

    [Test]
    public async Task CopyToLocalAsync_EncryptedClip_StaysEncryptedAndDecryptsLocallyWithSamePassphrase()
    {
        // Arrange
        var source = await _peerClipService.CreateAsync(_peerDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            Title = "Bank details",
            TextContent = "Sort code 00-00-00",
            ContentHash = "hash-secret",
            CapturedAt = DateTimeOffset.UtcNow,
            CollectionId = _peerCollectionId,
        });

        using var encryptionKey = EncryptionKey.FromPassphrase(_passphrase);
        await _peerClipService.EncryptClipsAsync(_peerDatabaseKey, [source.Id], encryptionKey);
        var encryptedSource = await _peerClipService.GetByIdAsync(_peerDatabaseKey, source.Id);

        // Act
        var copy = await _localClipService.CopyToLocalAsync(_remoteDatabaseKey, source.Id, _localDatabaseKey, _localCollectionId);

        // Assert - still encrypted, with the Peer's encryption fields
        var stored = await _localClipService.GetByIdAsync(_localDatabaseKey, copy.Id);
        await Assert.That(stored!.Encrypted).IsTrue();
        await Assert.That(stored.EncryptionSalt).IsEqualTo(encryptedSource!.EncryptionSalt);
        await Assert.That(stored.EncryptionIv).IsEqualTo(encryptedSource.EncryptionIv);
        await Assert.That(stored.EncryptionMethod).IsEqualTo(encryptedSource.EncryptionMethod);
        await Assert.That(stored.Title).IsNotEqualTo("Bank details");

        var storedText = (await new BlobRepository(DbContext).GetTextByClipIdAsync(copy.Id)).Single().Data;
        await Assert.That(storedText).IsNotEqualTo("Sort code 00-00-00");

        // Assert - decrypts locally with the same passphrase
        using var localKey = EncryptionKey.FromPassphrase(_passphrase);
        var decrypted = await _localClipService.DecryptClipsAsync(_localDatabaseKey, [copy.Id], localKey, true);
        await Assert.That(decrypted).IsEqualTo(1);

        var decryptedText = (await new BlobRepository(DbContext).GetTextByClipIdAsync(copy.Id)).Single().Data;
        await Assert.That(decryptedText).IsEqualTo("Sort code 00-00-00");
    }

    [Test]
    public async Task CopyToLocalAsync_LaterChangesOnThePeer_DoNotAffectTheLocalCopy()
    {
        // Arrange
        var source = await _peerClipService.CreateAsync(_peerDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            Title = "Original",
            TextContent = "Original text",
            ContentHash = "hash-original",
            CapturedAt = DateTimeOffset.UtcNow,
            CollectionId = _peerCollectionId,
        });

        var copy = await _localClipService.CopyToLocalAsync(_remoteDatabaseKey, source.Id, _localDatabaseKey, _localCollectionId);

        // Act - the Peer edits the clip's text and title, then deletes it
        var peerBlobs = new BlobRepository(_peerContext);
        var peerText = (await peerBlobs.GetTextByClipIdAsync(source.Id)).Single();
        peerText.Data = "Changed on the Peer";
        await peerBlobs.UpdateTextAsync(peerText);
        await _peerClipService.RenameClipAsync(_peerDatabaseKey, source.Id, "Renamed on the Peer");
        await _peerClipService.DeleteAsync(_peerDatabaseKey, source.Id);

        // Assert
        var stored = await _localClipService.GetByIdAsync(_localDatabaseKey, copy.Id);
        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.Title).IsEqualTo("Original");
        var localText = (await new BlobRepository(DbContext).GetTextByClipIdAsync(copy.Id)).Single().Data;
        await Assert.That(localText).IsEqualTo("Original text");
    }

    [Test]
    public async Task CopyToLocalAsync_ClipOfAUserOnThePeer_IsStoredWithoutAUserLikeACapturedClip()
    {
        // Arrange: a user ID only means something in the Peer's own database
        var source = await _peerClipService.CreateAsync(_peerDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            Title = "From another user",
            TextContent = "Hello",
            ContentHash = "hash-user",
            CapturedAt = DateTimeOffset.UtcNow,
            CollectionId = _peerCollectionId,
            UserId = 7,
        });

        // Act
        var copy = await _localClipService.CopyToLocalAsync(_remoteDatabaseKey, source.Id, _localDatabaseKey, _localCollectionId);

        // Assert
        var stored = await _localClipService.GetByIdAsync(_localDatabaseKey, copy.Id);
        await Assert.That(stored!.UserId).IsNull();
    }

    [Test]
    public async Task CopyToLocalAsync_SameClipTwice_CreatesTwoIndependentClips()
    {
        // Arrange
        var source = await _peerClipService.CreateAsync(_peerDatabaseKey, new Clip
        {
            Type = ClipType.Image,
            Title = "Screenshot",
            ImageData = [137, 80, 78, 71, 1, 2, 3],
            ContentHash = "hash-image",
            CapturedAt = DateTimeOffset.UtcNow,
            CollectionId = _peerCollectionId,
        });

        // Act
        var first = await _localClipService.CopyToLocalAsync(_remoteDatabaseKey, source.Id, _localDatabaseKey, _localCollectionId);
        var second = await _localClipService.CopyToLocalAsync(_remoteDatabaseKey, source.Id, _localDatabaseKey, _localCollectionId);

        // Assert
        await Assert.That(second.Id).IsNotEqualTo(first.Id);
        var blobs = new BlobRepository(DbContext);
        byte[] expected = [137, 80, 78, 71, 1, 2, 3];
        await Assert.That((await blobs.GetPngByClipIdAsync(first.Id)).Single().Data).IsEquivalentTo(expected);
        await Assert.That((await blobs.GetPngByClipIdAsync(second.Id)).Single().Data).IsEquivalentTo(expected);
    }

    [Test]
    public async Task CopyToLocalAsync_IntoARemoteDatabase_IsRefused()
    {
        // Arrange
        var source = await _peerClipService.CreateAsync(_peerDatabaseKey, new Clip
        {
            Type = ClipType.Text,
            TextContent = "x",
            ContentHash = "hash-x",
            CapturedAt = DateTimeOffset.UtcNow,
            CollectionId = _peerCollectionId,
        });

        // Act & Assert
        await Assert.That(async () => await _localClipService.CopyToLocalAsync(_remoteDatabaseKey, source.Id, _remoteDatabaseKey, _localCollectionId))
            .Throws<InvalidOperationException>();
    }

    private IDatabaseContextFactory CreateLocalOnlyFactory()
    {
        var factory = A.Fake<IDatabaseContextFactory>();
        Map(factory, _localDatabaseKey, DbContext);
        return factory;
    }

    private static void Map(IDatabaseContextFactory factory, string databaseKey, ClipMateDbContext context)
    {
        A.CallTo(() => factory.GetClipRepository(databaseKey)).Returns(new ClipRepository(context, A.Fake<ILogger<ClipRepository>>()));
        A.CallTo(() => factory.GetClipDataRepository(databaseKey)).Returns(new ClipDataRepository(context));
        A.CallTo(() => factory.GetBlobRepository(databaseKey)).Returns(new BlobRepository(context));
    }

    private static async Task<Guid> AddCollectionAsync(ClipMateDbContext context, string name)
    {
        var collection = new Collection { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        context.Collections.Add(collection);
        await context.SaveChangesAsync();
        return collection.Id;
    }

    private static ClipService CreateClipService(IDatabaseContextFactory factory, IClipContentService clipContentService)
    {
#pragma warning disable CA2000 // Test objects don't require explicit disposal
        var blobCacheService = new DecryptedBlobCacheService(new MemoryCache(new MemoryCacheOptions()),
            A.Fake<ILogger<DecryptedBlobCacheService>>());
#pragma warning restore CA2000

        return new ClipService(factory,
            A.Fake<IConfigurationService>(),
            A.Fake<IClipboardService>(),
            A.Fake<ITemplateService>(),
            new AesEncryptionService(),
            blobCacheService,
            clipContentService,
            A.Fake<IMessenger>(),
            A.Fake<ILogger<ClipService>>());
    }
}
