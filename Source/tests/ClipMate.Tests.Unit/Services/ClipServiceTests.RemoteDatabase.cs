using System.Text;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using ClipMate.Platform.Helpers;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Clips of a Remote Database: read from the Peer through the same service calls as local clips, never written.
/// </summary>
public partial class ClipServiceTests
{
    private static readonly RemoteDatabaseAddress _remoteAddress =
        new(DeviceId.FromCertificateBytes([9, 9]), Guid.Parse("44444444-4444-4444-4444-444444444444"));

    private static string RemoteKey => RemoteDatabaseKey.From(_remoteAddress);

    [Test]
    public async Task LoadAndSetClipboardAsync_RemoteClip_PutsThePeersContentOnTheClipboardWithTheHashThatSuppressesCapture()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, Title = "Hello" };
        var text = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.UnicodeText.Code, StorageType = StorageType.Text };
        A.CallTo(() => MockRemoteDatabases.GetClipContentAsync(_remoteAddress, clip.Id, A<CancellationToken>._))
            .Returns(new ClipContent(clip, [text], [new BlobTxt { ClipId = clip.Id, ClipDataId = text.Id, Data = "Hello from the desktop" }], [], [], []));
        Clip? placed = null;
        A.CallTo(() => MockClipboardService.SetClipboardContentAsync(A<Clip>._, A<CancellationToken>._))
            .Invokes(call => placed = call.GetArgument<Clip>(0));
        var service = CreateClipService();

        // Act
        await service.LoadAndSetClipboardAsync(RemoteKey, clip.Id);

        // Assert
        await Assert.That(placed).IsNotNull();
        await Assert.That(placed!.TextContent).IsEqualTo("Hello from the desktop");
        await Assert.That(placed.ContentHash).IsEqualTo(ContentHasher.HashText("Hello from the desktop"));
        A.CallTo(MockContextFactory).MustNotHaveHappened();
    }

    [Test]
    public async Task LoadBlobDataAsync_RemoteClip_FillsTheContentFromThePeer()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Image };
        var png = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.Dib.Code, StorageType = StorageType.Png };
        A.CallTo(() => MockRemoteDatabases.GetClipContentAsync(_remoteAddress, clip.Id, A<CancellationToken>._))
            .Returns(new ClipContent(clip, [png], [], [], [new BlobPng { ClipId = clip.Id, ClipDataId = png.Id, Data = [1, 2, 3] }], []));
        var service = CreateClipService();

        // Act
        await service.LoadBlobDataAsync(RemoteKey, clip);

        // Assert
        await Assert.That(clip.ImageData).IsEquivalentTo(new byte[] { 1, 2, 3 });
        A.CallTo(MockContextFactory).MustNotHaveHappened();
    }

    [Test]
    public async Task DecryptClipAsync_RemoteClipForViewing_DecryptsThePeersCiphertextOnThisDevice()
    {
        // Arrange
        var clip = CreateTestClip(encrypted: true, encryptionSalt: "c2FsdA==", encryptionIv: "aXY=");
        var text = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.UnicodeText.Code, StorageType = StorageType.Text };
        var ciphertext = Convert.ToBase64String([1, 2, 3, 4]);
        A.CallTo(() => MockRemoteDatabases.GetClipContentAsync(_remoteAddress, clip.Id, A<CancellationToken>._))
            .Returns(new ClipContent(clip, [text], [new BlobTxt { ClipId = clip.Id, ClipDataId = text.Id, Data = ciphertext }], [], [], []));
        A.CallTo(() => MockEncryptionService.DecryptAsync(A<byte[]>._, A<EncryptionMetadata>._, A<EncryptionKey>._, A<int>._))
            .Returns(Encoding.UTF8.GetBytes("secret"));
        DecryptedBlobData? cached = null;
        A.CallTo(() => MockBlobCacheService.CacheDecryptedBlobs(clip.Id, A<DecryptedBlobData>._, A<TimeSpan?>._, A<Action<Guid>?>._))
            .Invokes(call => cached = call.GetArgument<DecryptedBlobData>(1));
        var service = CreateClipService();

        // Act
        var result = await service.DecryptClipAsync(RemoteKey, clip, EncryptionKey.FromPassphrase("passphrase", 5));

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(clip.IsDecrypted).IsTrue();
        await Assert.That(cached!.TextBlobs.Single().Data).IsEqualTo("secret");
        A.CallTo(MockContextFactory).MustNotHaveHappened();
    }

    [Test]
    public async Task DecryptClipAsync_RemoteClipPermanently_IsRefusedBecauseRemoteDatabasesAreReadOnly()
    {
        // Arrange
        var clip = CreateTestClip(encrypted: true, encryptionSalt: "c2FsdA==", encryptionIv: "aXY=");
        var service = CreateClipService();

        // Act / Assert
        await Assert.That(async () => await service.DecryptClipAsync(RemoteKey, clip, EncryptionKey.FromPassphrase("passphrase", 5), true))
            .Throws<InvalidOperationException>();
        await Assert.That(clip.Encrypted).IsTrue();
        A.CallTo(MockEncryptionService).MustNotHaveHappened();
    }
}
