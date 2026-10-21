using System.Net;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using ClipMate.Networking;
using ClipMate.Networking.Protocol;
using ClipMate.Networking.SharedDatabases;
using ClipMate.Platform.Services;
using FakeItEasy;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Integration.Networking;

/// <summary>
/// The Shared Database calls between two in-process Devices over loopback mutual TLS: the desktop shares, the laptop asks.
/// </summary>
[NotInParallel]
public class SharedDatabaseTransportTests
{
    private static readonly Guid _workId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static string? _identityDirectory;
    private static DeviceIdentity? _laptopIdentity;
    private static DeviceIdentity? _desktopIdentity;
    private GrpcSharedDatabaseClient _client = null!;
    private IDeviceDiscoveryService _discovery = null!;
    private IPeerHost _desktopHost = null!;
    private PeerStore _desktopStore = null!;
    private PeerStore _laptopStore = null!;
    private ISharedDatabaseService _sharedDatabases = null!;

    [Before(Class)]
    public static async Task CreateIdentities()
    {
        _identityDirectory = Path.Combine(Path.GetTempPath(), $"clipmate_shared_{Guid.NewGuid():N}");
        _laptopIdentity = await new DeviceIdentityService(Path.Combine(_identityDirectory, "laptop"), NullLogger<DeviceIdentityService>.Instance)
            .GetOrCreateAsync();
        _desktopIdentity = await new DeviceIdentityService(Path.Combine(_identityDirectory, "desktop"), NullLogger<DeviceIdentityService>.Instance)
            .GetOrCreateAsync();
    }

    [After(Class)]
    public static void DeleteIdentities()
    {
        _laptopIdentity?.Dispose();
        _desktopIdentity?.Dispose();
        if (_identityDirectory != null && Directory.Exists(_identityDirectory))
            Directory.Delete(_identityDirectory, true);
    }

    [Before(Test)]
    public async Task StartDesktop()
    {
        _desktopStore = new PeerStore(CreateConfigurationService("DESKTOP"), NullLogger<PeerStore>.Instance);
        _laptopStore = new PeerStore(CreateConfigurationService("LAPTOP"), NullLogger<PeerStore>.Instance);
        _sharedDatabases = A.Fake<ISharedDatabaseService>();

        // Loopback only, so the test run never triggers a Windows Firewall prompt.
        _desktopHost = new KestrelPeerHostFactory(new PeerStoreCertificateValidator(_desktopStore), _desktopStore, A.Fake<IPairingApprovalHandler>(),
            CreateConfigurationService("DESKTOP"), _sharedDatabases, NullLoggerFactory.Instance,
            new PeerHostOptions { ListenAddress = IPAddress.Loopback }).Create();
        await _desktopHost.StartAsync(_desktopIdentity!, 0);

        _discovery = A.Fake<IDeviceDiscoveryService>();
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([]);
        _client = new GrpcSharedDatabaseClient(_discovery, NullLogger<GrpcSharedDatabaseClient>.Instance);
    }

    [After(Test)]
    public async Task StopDesktop() => await _desktopHost.DisposeAsync();

    private string DesktopAddress => new IPEndPoint(IPAddress.Loopback, _desktopHost.ListeningPort!.Value).ToString();

    [Test]
    public async Task ListSharedDatabasesAsync_FromAPeer_ReturnsTheSharedDatabases()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.GetSharedDatabasesAsync(A<CancellationToken>._)).Returns([new SharedDatabase(_workId, "Work")]);

        // Act
        var databases = await _client.ListSharedDatabasesAsync(_laptopIdentity!, desktop);

        // Assert
        await Assert.That(databases).IsEquivalentTo([new SharedDatabase(_workId, "Work")]);
    }

    [Test]
    public async Task GetCollectionTreeAsync_FromAPeer_ReturnsTheTreeIncludingSmartAndSqlBasedCollections()
    {
        // Arrange
        var desktop = await PairAsync();
        var inbox = new SharedCollection(Guid.NewGuid(), null, "Inbox", SharedCollectionKind.Collection, 10);
        var tree = new[]
        {
            inbox,
            new SharedCollection(Guid.NewGuid(), inbox.Id, "Projects", SharedCollectionKind.Folder, 5),
            new SharedCollection(Guid.NewGuid(), null, "Today", SharedCollectionKind.Smart, 30),
            new SharedCollection(Guid.NewGuid(), null, "Bitmaps", SharedCollectionKind.SqlBased, 40),
        };
        A.CallTo(() => _sharedDatabases.GetCollectionTreeAsync(_workId, A<CancellationToken>._)).Returns(tree);

        // Act
        var result = await _client.GetCollectionTreeAsync(_laptopIdentity!, desktop, _workId);

        // Assert
        await Assert.That(result).IsEquivalentTo(tree);
    }

    [Test]
    public async Task GetCollectionTreeAsync_DatabaseNotShared_ReturnsNull()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.GetCollectionTreeAsync(A<Guid>._, A<CancellationToken>._)).Returns((IReadOnlyList<SharedCollection>?)null);

        // Act
        var result = await _client.GetCollectionTreeAsync(_laptopIdentity!, desktop, _workId);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ListSharedDatabasesAsync_FromADeviceThatIsNotAPeer_IsRefusedAndServesNothing()
    {
        // Arrange: the laptop knows the desktop, but the desktop never paired with the laptop.
        await _laptopStore.SavePairedAsync(_desktopIdentity!.DeviceId, "DESKTOP", null, DesktopAddress);
        var desktop = _laptopStore.Find(_desktopIdentity.DeviceId)!;

        // Act / Assert
        await Assert.That(async () => await _client.ListSharedDatabasesAsync(_laptopIdentity!, desktop)).Throws<RemoteDatabaseUnavailableException>();
        A.CallTo(() => _sharedDatabases.GetSharedDatabasesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ListSharedDatabases_OnThePairingPathFromADeviceThatIsNotAPeer_IsPermissionDenied()
    {
        // Arrange
        using var channel = PeerChannelFactory.CreateForPairing(DesktopAddress, _laptopIdentity!, _desktopIdentity!.DeviceId);
        var client = new SharedDatabaseAccess.SharedDatabaseAccessClient(channel);

        // Act / Assert
        var exception = await Assert.That(async () => await client.ListSharedDatabasesAsync(new ListSharedDatabasesRequest())).Throws<RpcException>();
        await Assert.That(exception!.StatusCode).IsEqualTo(StatusCode.PermissionDenied);
        A.CallTo(() => _sharedDatabases.GetSharedDatabasesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ListSharedDatabasesAsync_PeerNotListening_ThrowsRemoteDatabaseUnavailable()
    {
        // Arrange
        var desktop = await PairAsync();
        await _desktopHost.StopAsync();

        // Act / Assert
        await Assert.That(async () => await _client.ListSharedDatabasesAsync(_laptopIdentity!, desktop)).Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task GetClipPageAsync_FromAPeer_ReturnsThePageOfClipMetadata()
    {
        // Arrange
        var desktop = await PairAsync();
        var collectionId = Guid.NewGuid();
        var clip = new Clip
        {
            Id = Guid.NewGuid(), CollectionId = collectionId, Title = "Quarterly report", CapturedAt = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(2)),
            Size = 2048, Type = ClipType.Html, HasText = true, HasHtml = true, SourceApplicationName = @"C:\Apps\WINWORD.EXE",
            SourceApplicationTitle = "report.docx", SortKey = 7,
        };
        A.CallTo(() => _sharedDatabases.GetClipPageAsync(_workId, collectionId, null, 0, 200, A<CancellationToken>._))
            .Returns(new ClipPage([clip], 0, 4212));

        // Act
        var page = await _client.GetClipPageAsync(_laptopIdentity!, desktop, _workId, collectionId, null, 0, 200);

        // Assert
        await Assert.That(page).IsNotNull();
        await Assert.That(page!.TotalCount).IsEqualTo(4212);
        var received = page.Clips.Single();
        await Assert.That(received.Id).IsEqualTo(clip.Id);
        await Assert.That(received.Title).IsEqualTo("Quarterly report");
        await Assert.That(received.CapturedAt).IsEqualTo(clip.CapturedAt);
        await Assert.That(received.Size).IsEqualTo(2048);
        await Assert.That(received.Type).IsEqualTo(ClipType.Html);
        await Assert.That(received.HasHtml && received.HasText && !received.HasBitmap).IsTrue();
        await Assert.That(received.SourceApplicationName).IsEqualTo(@"C:\Apps\WINWORD.EXE");
        await Assert.That(received.IconGlyph).IsEqualTo("🌐📄");
    }

    [Test]
    public async Task GetClipPageAsync_CollectionNotShared_ReturnsNull()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.GetClipPageAsync(A<Guid>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .Returns((ClipPage?)null);

        // Act
        var page = await _client.GetClipPageAsync(_laptopIdentity!, desktop, _workId, Guid.NewGuid(), null, 0, 200);

        // Assert
        await Assert.That(page).IsNull();
    }

    [Test]
    public async Task GetClipPageAsync_AskingForAHugePage_IsCappedByThePeer()
    {
        // Arrange
        var desktop = await PairAsync();
        var collectionId = Guid.NewGuid();

        // Act
        await _client.GetClipPageAsync(_laptopIdentity!, desktop, _workId, collectionId, null, 0, 1_000_000);

        // Assert
        A.CallTo(() => _sharedDatabases.GetClipPageAsync(_workId, collectionId, null, 0, A<int>.That.Matches(p => p <= 500), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task GetClipContentAsync_LargeImage_IsStreamedInChunksAndArrivesWhole()
    {
        // Arrange: bigger than gRPC's default 4 MB message limit, so it only arrives if it is chunked.
        var desktop = await PairAsync();
        var image = new byte[6 * 1024 * 1024];
        new Random(42).NextBytes(image);
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Screenshot", Type = ClipType.Image };
        var png = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = 8, FormatName = "CF_DIB", StorageType = StorageType.Png, Size = image.Length };
        var text = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = 13, FormatName = "CF_UNICODETEXT", StorageType = StorageType.Text };
        A.CallTo(() => _sharedDatabases.GetClipContentAsync(_workId, clip.Id, A<CancellationToken>._)).Returns(new ClipContent(clip, [png, text],
            [new BlobTxt { ClipId = clip.Id, ClipDataId = text.Id, Data = "Grüße ✓" }], [], [new BlobPng { ClipId = clip.Id, ClipDataId = png.Id, Data = image }], []));

        // Act
        var content = await _client.GetClipContentAsync(_laptopIdentity!, desktop, _workId, clip.Id);

        // Assert
        await Assert.That(content).IsNotNull();
        await Assert.That(content!.Clip.Title).IsEqualTo("Screenshot");
        await Assert.That(content.Formats.Select(p => p.Id)).IsEquivalentTo([png.Id, text.Id]);
        await Assert.That(content.PngBlobs.Single().Data.SequenceEqual(image)).IsTrue();
        await Assert.That(content.PngBlobs.Single().ClipDataId).IsEqualTo(png.Id);
        await Assert.That(content.TextBlobs.Single().Data).IsEqualTo("Grüße ✓");
    }

    [Test]
    public async Task GetClipContentAsync_EncryptedClip_ArrivesAsCiphertextWithItsEncryptionMetadata()
    {
        // Arrange
        var desktop = await PairAsync();
        var clip = new Clip
        {
            Id = Guid.NewGuid(), Type = ClipType.Text, Encrypted = true, EncryptionSalt = "c2FsdA==", EncryptionIv = "aXY=", EncryptionMethod = "AES-256-CBC",
        };
        var text = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = 13, FormatName = "CF_UNICODETEXT", StorageType = StorageType.Text };
        A.CallTo(() => _sharedDatabases.GetClipContentAsync(_workId, clip.Id, A<CancellationToken>._))
            .Returns(new ClipContent(clip, [text], [new BlobTxt { ClipId = clip.Id, ClipDataId = text.Id, Data = "Q0lQSEVSVEVYVA==" }], [], [], []));

        // Act
        var content = await _client.GetClipContentAsync(_laptopIdentity!, desktop, _workId, clip.Id);

        // Assert
        await Assert.That(content!.Clip.Encrypted).IsTrue();
        await Assert.That(content.Clip.EncryptionSalt).IsEqualTo("c2FsdA==");
        await Assert.That(content.Clip.EncryptionIv).IsEqualTo("aXY=");
        await Assert.That(content.Clip.EncryptionMethod).IsEqualTo("AES-256-CBC");
        await Assert.That(content.TextBlobs.Single().Data).IsEqualTo("Q0lQSEVSVEVYVA==");
    }

    [Test]
    public async Task GetClipContentAsync_ClipNotShared_ReturnsNull()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.GetClipContentAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns((ClipContent?)null);

        // Act
        var content = await _client.GetClipContentAsync(_laptopIdentity!, desktop, _workId, Guid.NewGuid());

        // Assert
        await Assert.That(content).IsNull();
    }

    [Test]
    public async Task GetClipPageAsync_PeerTooSlow_TimesOutAsUnavailableAndCancelsTheWorkOnThePeer()
    {
        // Arrange
        var desktop = await PairAsync();
        var peerCancelled = new TaskCompletionSource();
        A.CallTo(() => _sharedDatabases.GetClipPageAsync(A<Guid>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                var token = call.GetArgument<CancellationToken>(5);
                token.Register(() => peerCancelled.TrySetResult());
                await Task.Delay(Timeout.Infinite, token);
                return (ClipPage?)null;
            });
        var client = new GrpcSharedDatabaseClient(_discovery, NullLogger<GrpcSharedDatabaseClient>.Instance, TimeSpan.FromMilliseconds(500));

        // Act / Assert
        await Assert.That(async () => await client.GetClipPageAsync(_laptopIdentity!, desktop, _workId, Guid.NewGuid(), null, 0, 200))
            .Throws<RemoteDatabaseUnavailableException>();
        await peerCancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task GetClipPageAsync_PeerNotListening_ThrowsRemoteDatabaseUnavailable()
    {
        // Arrange
        var desktop = await PairAsync();
        await _desktopHost.StopAsync();

        // Act / Assert
        await Assert.That(async () => await _client.GetClipPageAsync(_laptopIdentity!, desktop, _workId, Guid.NewGuid(), null, 0, 200))
            .Throws<RemoteDatabaseUnavailableException>();
    }

    private async Task<Peer> PairAsync()
    {
        await _desktopStore.SavePairedAsync(_laptopIdentity!.DeviceId, "LAPTOP", null, null);
        await _laptopStore.SavePairedAsync(_desktopIdentity!.DeviceId, "DESKTOP", null, DesktopAddress);
        return _laptopStore.Find(_desktopIdentity.DeviceId)!;
    }

    private static IConfigurationService CreateConfigurationService(string deviceName)
    {
        var configuration = new ClipMateConfiguration { Networking = { DeviceName = deviceName } };
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(configuration);
        return configurationService;
    }
}
