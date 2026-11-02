using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Where a capture goes: the active collection, or after ClipMate 7's bounce another one. A capture is never refused for
/// its collection.
/// </summary>
public class CaptureTargetResolverTests
{
    private ICollectionService _collections = null!;
    private IFolderService _folders = null!;
    private CaptureTargetResolver _resolver = null!;

    [Before(Test)]
    public void Setup()
    {
        _collections = A.Fake<ICollectionService>();
        A.CallTo(() => _collections.GetActiveDatabaseKey()).Returns("db");
        _folders = A.Fake<IFolderService>();
        A.CallTo(() => _folders.GetActiveAsync(A<CancellationToken>._)).Returns((Folder?)null);
        _resolver = new CaptureTargetResolver(_collections, _folders, A.Fake<ILogger<CaptureTargetResolver>>());
    }

    [Test]
    public async Task ActiveCollectionThatAcceptsCaptures_IsTheTarget()
    {
        // Arrange
        var inbox = Collection("Inbox");
        A.CallTo(() => _collections.GetActiveAsync(A<CancellationToken>._)).Returns(inbox);

        // Act
        var target = await _resolver.ResolveAsync();

        // Assert
        await Assert.That(target!.Collection).IsSameReferenceAs(inbox);
        await Assert.That(target.DatabaseKey).IsEqualTo("db");
    }

    [Test]
    public async Task ReadOnlyCollection_BouncesToTheFirstThatAcceptsCaptures()
    {
        // Arrange: ClipMate 7 treats Read-only like Accept New Clips off; we used to drop the capture
        var safe = Collection("Safe");
        safe.ReadOnly = true;
        var inbox = Collection("Inbox");
        A.CallTo(() => _collections.GetActiveAsync(A<CancellationToken>._)).Returns(safe);
        A.CallTo(() => _collections.GetFirstAcceptingCollectionAsync(A<CancellationToken>._)).Returns(inbox);

        // Act
        var target = await _resolver.ResolveAsync();

        // Assert
        await Assert.That(target!.Collection).IsSameReferenceAs(inbox);
    }

    [Test]
    public async Task NoCollectionAcceptsCaptures_TheFirstCollectionTakesIt()
    {
        // Arrange: ClipMate 7 never refuses a capture for its collection
        var safe = Collection("Safe", sortKey: 2);
        safe.AcceptNewClips = false;
        var first = Collection("Archive", sortKey: 1);
        first.AcceptNewClips = false;
        A.CallTo(() => _collections.GetActiveAsync(A<CancellationToken>._)).Returns(safe);
        A.CallTo(() => _collections.GetFirstAcceptingCollectionAsync(A<CancellationToken>._)).Returns((Collection?)null);
        A.CallTo(() => _collections.GetAllAsync(A<CancellationToken>._)).Returns([safe, first]);

        // Act
        var target = await _resolver.ResolveAsync();

        // Assert
        await Assert.That(target!.Collection).IsSameReferenceAs(first);
    }

    [Test]
    public async Task ActiveFolderOfAnotherCollection_IsNotUsedAfterABounce()
    {
        // Arrange: the active folder belongs to the collection the capture bounced away from
        var safe = Collection("Safe");
        safe.AcceptNewClips = false;
        var inbox = Collection("Inbox");
        var inboxFolder = new Folder { Id = Guid.NewGuid(), CollectionId = inbox.Id, FolderType = FolderType.Inbox };
        A.CallTo(() => _collections.GetActiveAsync(A<CancellationToken>._)).Returns(safe);
        A.CallTo(() => _collections.GetFirstAcceptingCollectionAsync(A<CancellationToken>._)).Returns(inbox);
        A.CallTo(() => _folders.GetActiveAsync(A<CancellationToken>._)).Returns(new Folder { Id = Guid.NewGuid(), CollectionId = safe.Id });
        A.CallTo(() => _folders.GetRootFoldersAsync(inbox.Id, A<CancellationToken>._)).Returns([inboxFolder]);

        // Act
        var target = await _resolver.ResolveAsync();

        // Assert
        await Assert.That(target!.FolderId).IsEqualTo(inboxFolder.Id);
    }

    [Test]
    public async Task NoActiveDatabase_HasNoTarget()
    {
        // Arrange
        A.CallTo(() => _collections.GetActiveDatabaseKey()).Returns(null);

        // Act & Assert
        await Assert.That(await _resolver.ResolveAsync()).IsNull();
    }

    private static Collection Collection(string title, int sortKey = 0) =>
        new() { Id = Guid.NewGuid(), Title = title, AcceptNewClips = true, SortKey = sortKey };
}
