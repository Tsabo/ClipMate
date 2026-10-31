using System.Net;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using ClipMate.Networking;
using ClipMate.Networking.Protocol;
using ClipMate.Networking.SharedDatabases;
using ClipMate.Networking.SharedDatabases.Notice;
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
    private ISharedDatabaseChangeFeed _changeFeed = null!;

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
        _changeFeed = A.Fake<ISharedDatabaseChangeFeed>();

        // Loopback only, so the test run never triggers a Windows Firewall prompt.
        _desktopHost = new KestrelPeerHostFactory(new PeerStoreCertificateValidator(_desktopStore), _desktopStore, A.Fake<IPairingApprovalHandler>(),
            CreateConfigurationService("DESKTOP"), _sharedDatabases, _changeFeed, NullLoggerFactory.Instance,
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

    [Test]
    [Arguments(StatusCode.ResourceExhausted)]
    [Arguments(StatusCode.InvalidArgument)]
    [Arguments(StatusCode.Internal)]
    [Arguments(StatusCode.Unknown)]
    [Arguments(StatusCode.FailedPrecondition)]
    [Arguments(StatusCode.Unauthenticated)]
    public async Task GetClipPageAsync_PeerAnswersWithAnUnexpectedError_ThrowsRemoteDatabaseUnavailable(StatusCode status)
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.GetClipPageAsync(A<Guid>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .Throws(new RpcException(new Status(status, "Something went wrong.")));

        // Act / Assert
        await Assert.That(async () => await _client.GetClipPageAsync(_laptopIdentity!, desktop, _workId, Guid.NewGuid(), null, 0, 200))
            .Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task GetClipPageAsync_PeerFailsWhileAnswering_ThrowsRemoteDatabaseUnavailable()
    {
        // Arrange: an unexpected exception on the Peer reaches this Device as status Unknown.
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.GetClipPageAsync(A<Guid>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Database is locked."));

        // Act / Assert
        await Assert.That(async () => await _client.GetClipPageAsync(_laptopIdentity!, desktop, _workId, Guid.NewGuid(), null, 0, 200))
            .Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task GetClipPageAsync_PeerAnswersWithAnError_DoesNotTryItsOtherAddresses()
    {
        // Arrange: the Peer is announced at one address and remembered at another that reaches the same listener.
        await _desktopStore.SavePairedAsync(_laptopIdentity!.DeviceId, "LAPTOP", null, null);
        await _laptopStore.SavePairedAsync(_desktopIdentity!.DeviceId, "DESKTOP", null, $"localhost:{_desktopHost.ListeningPort}");
        var desktop = _laptopStore.Find(_desktopIdentity.DeviceId)!;
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([
            new DiscoveredDevice(_desktopIdentity.DeviceId, "DESKTOP", new IPEndPoint(IPAddress.Loopback, _desktopHost.ListeningPort!.Value), PeerProtocol.Major, 0,
                DateTimeOffset.UtcNow),
        ]);
        A.CallTo(() => _sharedDatabases.GetClipPageAsync(A<Guid>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .Throws(new RpcException(new Status(StatusCode.Internal, "Something went wrong.")));

        // Act / Assert
        await Assert.That(async () => await _client.GetClipPageAsync(_laptopIdentity!, desktop, _workId, Guid.NewGuid(), null, 0, 200))
            .Throws<RemoteDatabaseUnavailableException>();
        A.CallTo(() => _sharedDatabases.GetClipPageAsync(A<Guid>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ListSharedDatabasesAsync_PeerAnswersWithAnUnexpectedError_ThrowsRemoteDatabaseUnavailable()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.GetSharedDatabasesAsync(A<CancellationToken>._))
            .Throws(new RpcException(new Status(StatusCode.ResourceExhausted, "Too busy.")));

        // Act / Assert
        await Assert.That(async () => await _client.ListSharedDatabasesAsync(_laptopIdentity!, desktop)).Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task GetCollectionTreeAsync_PeerAnswersWithAnUnexpectedError_ThrowsRemoteDatabaseUnavailable()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.GetCollectionTreeAsync(A<Guid>._, A<CancellationToken>._))
            .Throws(new RpcException(new Status(StatusCode.Internal, "Something went wrong.")));

        // Act / Assert
        await Assert.That(async () => await _client.GetCollectionTreeAsync(_laptopIdentity!, desktop, _workId)).Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task GetClipContentAsync_PeerAnswersWithAnUnexpectedError_ThrowsRemoteDatabaseUnavailable()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.GetClipContentAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._))
            .Throws(new RpcException(new Status(StatusCode.ResourceExhausted, "Too busy.")));

        // Act / Assert
        await Assert.That(async () => await _client.GetClipContentAsync(_laptopIdentity!, desktop, _workId, Guid.NewGuid()))
            .Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task WatchChangesAsync_ACollectionChangesOnThePeer_DeliversOnlyWhichCollectionChanged()
    {
        // Arrange
        var desktop = await PairAsync();
        var collectionId = Guid.NewGuid();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var notices = _client.WatchChangesAsync(_laptopIdentity!, desktop, stop.Token).GetAsyncEnumerator(stop.Token);
        var next = notices.MoveNextAsync().AsTask();
        await WaitForSubscriberAsync();

        // Act
        _changeFeed.CollectionChanged += Raise.With(new SharedCollectionChangedEventArgs(new SharedCollectionChange(_workId, collectionId)));

        // Assert
        await Assert.That(await next).IsTrue();
        await Assert.That(notices.Current).IsEqualTo(new CollectionChangedNotice(new SharedCollectionChange(_workId, collectionId)));
    }

    [Test]
    public async Task WatchChangesAsync_ABurstOfChangesToOneCollection_ArrivesAsOneNotice()
    {
        // Arrange
        var desktop = await PairAsync();
        var collectionId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var notices = _client.WatchChangesAsync(_laptopIdentity!, desktop, stop.Token).GetAsyncEnumerator(stop.Token);
        var next = notices.MoveNextAsync().AsTask();
        await WaitForSubscriberAsync();

        // Act
        for (var item = 0; item < 5; item++)
            _changeFeed.CollectionChanged += Raise.With(new SharedCollectionChangedEventArgs(new SharedCollectionChange(_workId, collectionId)));
        _changeFeed.CollectionChanged += Raise.With(new SharedCollectionChangedEventArgs(new SharedCollectionChange(_workId, otherId)));

        // Assert
        var received = new List<PeerChangeNotice>();
        await Assert.That(await next).IsTrue();
        received.Add(notices.Current);
        await Assert.That(await notices.MoveNextAsync()).IsTrue();
        received.Add(notices.Current);
        await Assert.That(received).IsEquivalentTo(new PeerChangeNotice[]
        {
            new CollectionChangedNotice(new SharedCollectionChange(_workId, collectionId)),
            new CollectionChangedNotice(new SharedCollectionChange(_workId, otherId)),
        });
    }

    [Test]
    public async Task WatchChangesAsync_ThePeerSharesOrUnsharesADatabase_DeliversSharedDatabasesChanged()
    {
        // Arrange
        var desktop = await PairAsync();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var notices = _client.WatchChangesAsync(_laptopIdentity!, desktop, stop.Token).GetAsyncEnumerator(stop.Token);
        var next = notices.MoveNextAsync().AsTask();
        await WaitForSubscriberAsync();

        // Act
        _changeFeed.SharedDatabasesChanged += Raise.WithEmpty();

        // Assert
        await Assert.That(await next).IsTrue();
        await Assert.That(notices.Current).IsTypeOf<SharedDatabasesChangedNotice>();
    }

    [Test]
    public async Task WatchChangesAsync_ThePeerUnpairsThisDevice_EndsTheStreamAndStopsListening()
    {
        // Arrange
        var desktop = await PairAsync();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var notices = _client.WatchChangesAsync(_laptopIdentity!, desktop, stop.Token).GetAsyncEnumerator(stop.Token);
        var next = notices.MoveNextAsync().AsTask();
        await WaitForSubscriberAsync();

        // Act
        await _desktopStore.RemoveAsync(_laptopIdentity!.DeviceId);

        // Assert
        await Assert.That(async () => await next).Throws<RemoteDatabaseUnavailableException>();
        await WaitForUnsubscribeAsync();
    }

    [Test]
    public async Task WatchChangesAsync_FromADeviceThatIsNotAPeer_IsRefused()
    {
        // Arrange: the laptop knows the desktop, but the desktop never paired with the laptop.
        await _laptopStore.SavePairedAsync(_desktopIdentity!.DeviceId, "DESKTOP", null, DesktopAddress);
        var desktop = _laptopStore.Find(_desktopIdentity.DeviceId)!;

        // Act / Assert
        await Assert.That(async () =>
        {
            await foreach (var item in _client.WatchChangesAsync(_laptopIdentity!, desktop))
            {
            }
        }).Throws<RemoteDatabaseUnavailableException>();
        await Assert.That(Fake.GetCalls(_changeFeed).Any(p => p.Method.Name.StartsWith("add_", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task WatchChangesAsync_Cancelled_StopsListeningOnThePeer()
    {
        // Arrange
        var desktop = await PairAsync();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var notices = _client.WatchChangesAsync(_laptopIdentity!, desktop, stop.Token).GetAsyncEnumerator(stop.Token);
        var next = notices.MoveNextAsync().AsTask();
        await WaitForSubscriberAsync();

        // Act
        await stop.CancelAsync();

        // Assert
        await Assert.That(async () => await next).Throws<OperationCanceledException>();
        await notices.DisposeAsync();
        await WaitForUnsubscribeAsync();
    }

    private async Task WaitForSubscriberAsync()
    {
        await WaitForCallsAsync("add_", 2);

        // The call is recorded just before the handler is attached
        await Task.Delay(50);
    }

    private async Task WaitForUnsubscribeAsync() => await WaitForCallsAsync("remove_", 2);

    private async Task WaitForCallsAsync(string prefix, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (Fake.GetCalls(_changeFeed).Count(p => p.Method.Name.StartsWith(prefix, StringComparison.Ordinal)) < count && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        await Assert.That(Fake.GetCalls(_changeFeed).Count(p => p.Method.Name.StartsWith(prefix, StringComparison.Ordinal))).IsGreaterThanOrEqualTo(count);
    }

    [Test]
    public async Task SearchAsync_FromAPeer_SendsTheCriteriaAndReceivesTheMatchesInPagesWithTheTotal()
    {
        // Arrange
        var desktop = await PairAsync();
        var collectionId = Guid.NewGuid();
        var clips = Enumerable.Range(0, 450).Select(p => new Clip { Id = Guid.NewGuid(), Title = $"report {p}", SortKey = 1000 - p }).ToList();
        SearchFilters? received = null;
        A.CallTo(() => _sharedDatabases.SearchAsync(_workId, A<SearchFilters>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                received = call.GetArgument<SearchFilters>(1);
                return Task.FromResult<SearchMatches?>(new SearchMatches(clips, 4212));
            });

        // Act
        var matches = await _client.SearchAsync(_laptopIdentity!, desktop, _workId, new SearchFilters
        {
            TitleQuery = "report", CreatorQuery = "jeremy", ContentTypes = [ClipType.Text], Scope = SearchScope.CurrentCollection,
            CollectionId = collectionId, EncryptedOnly = true,
        });

        // Assert
        await Assert.That(matches).IsNotNull();
        await Assert.That(matches!.TotalMatches).IsEqualTo(4212);
        await Assert.That(matches.Clips.Select(p => p.Id)).IsEquivalentTo(clips.Select(p => p.Id));
        await Assert.That(matches.Clips[0].Title).IsEqualTo("report 0");
        await Assert.That(received!.TitleQuery).IsEqualTo("report");
        await Assert.That(received.CreatorQuery).IsEqualTo("jeremy");
        await Assert.That(received.ContentTypes!).IsEquivalentTo([ClipType.Text]);
        await Assert.That(received.Scope).IsEqualTo(SearchScope.CurrentCollection);
        await Assert.That(received.CollectionId).IsEqualTo(collectionId);
        await Assert.That(received.EncryptedOnly == true).IsTrue();
    }

    [Test]
    public async Task SearchAsync_MoreThanTheCapMatch_ReturnsAtMost1000AndTheTotal()
    {
        // Arrange
        var desktop = await PairAsync();
        var clips = Enumerable.Range(0, 1500).Select(_ => new Clip { Id = Guid.NewGuid(), Title = "report" }).ToList();
        A.CallTo(() => _sharedDatabases.SearchAsync(_workId, A<SearchFilters>._, A<CancellationToken>._)).Returns(new SearchMatches(clips, 1500));

        // Act
        var matches = await _client.SearchAsync(_laptopIdentity!, desktop, _workId, new SearchFilters { TitleQuery = "report" });

        // Assert
        await Assert.That(matches!.Clips.Count).IsEqualTo(1000);
        await Assert.That(matches.TotalMatches).IsEqualTo(1500);
        await Assert.That(matches.IsTruncated).IsTrue();
    }

    [Test]
    public async Task SearchAsync_NoMatches_ReturnsEmptyResults()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.SearchAsync(_workId, A<SearchFilters>._, A<CancellationToken>._)).Returns(new SearchMatches([], 0));

        // Act
        var matches = await _client.SearchAsync(_laptopIdentity!, desktop, _workId, new SearchFilters { TitleQuery = "nothing" });

        // Assert
        await Assert.That(matches!.Clips).IsEmpty();
        await Assert.That(matches.TotalMatches).IsEqualTo(0);
    }

    [Test]
    public async Task SearchAsync_DatabaseNotShared_ReturnsNull()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.SearchAsync(A<Guid>._, A<SearchFilters>._, A<CancellationToken>._)).Returns((SearchMatches?)null);

        // Act
        var matches = await _client.SearchAsync(_laptopIdentity!, desktop, _workId, new SearchFilters { TitleQuery = "report" });

        // Assert
        await Assert.That(matches).IsNull();
    }

    [Test]
    public async Task SearchAsync_SearchRunsPastThePeersTimeout_IsStoppedOnThePeerAndReportedAsUnavailable()
    {
        // Arrange: a Peer whose search time limit is short, so the test does not wait 15 seconds
        await using var impatientHost = new KestrelPeerHostFactory(new PeerStoreCertificateValidator(_desktopStore), _desktopStore,
            A.Fake<IPairingApprovalHandler>(), CreateConfigurationService("DESKTOP"), _sharedDatabases, _changeFeed,
            NullLoggerFactory.Instance, new PeerHostOptions { ListenAddress = IPAddress.Loopback, SearchTimeout = TimeSpan.FromMilliseconds(300) }).Create();
        await impatientHost.StartAsync(_desktopIdentity!, 0);
        await _desktopStore.SavePairedAsync(_laptopIdentity!.DeviceId, "LAPTOP", null, null);
        await _laptopStore.SavePairedAsync(_desktopIdentity!.DeviceId, "DESKTOP", null,
            new IPEndPoint(IPAddress.Loopback, impatientHost.ListeningPort!.Value).ToString());
        var desktop = _laptopStore.Find(_desktopIdentity.DeviceId)!;
        var peerCancelled = WhenPeerSearchIsCancelled();

        // Act / Assert
        var exception = await Assert.That(async () => await _client.SearchAsync(_laptopIdentity!, desktop, _workId, new SearchFilters { TitleQuery = "slow" }))
            .Throws<RemoteDatabaseUnavailableException>();
        await Assert.That(exception!.Message).Contains("too long");
        await peerCancelled.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task SearchAsync_PeerFailsWhileSearching_ThrowsRemoteDatabaseUnavailable()
    {
        // Arrange
        var desktop = await PairAsync();
        A.CallTo(() => _sharedDatabases.SearchAsync(_workId, A<SearchFilters>._, A<CancellationToken>._))
            .Throws(new RpcException(new Status(StatusCode.Internal, "Something went wrong.")));

        // Act / Assert
        await Assert.That(async () => await _client.SearchAsync(_laptopIdentity!, desktop, _workId, new SearchFilters { TitleQuery = "x" }))
            .Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task SearchAsync_RequesterCancels_StopsTheSearchOnThePeer()
    {
        // Arrange
        var desktop = await PairAsync();
        var peerCancelled = WhenPeerSearchIsCancelled();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act / Assert
        await Assert.That(async () => await _client.SearchAsync(_laptopIdentity!, desktop, _workId, new SearchFilters { TitleQuery = "slow" }, cancellation.Token))
            .Throws<OperationCanceledException>();
        await peerCancelled.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private Task WhenPeerSearchIsCancelled()
    {
        var peerCancelled = new TaskCompletionSource();
        A.CallTo(() => _sharedDatabases.SearchAsync(A<Guid>._, A<SearchFilters>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                var token = call.GetArgument<CancellationToken>(2);
                token.Register(() => peerCancelled.TrySetResult());
                await Task.Delay(Timeout.Infinite, token);
                return (SearchMatches?)null;
            });

        return peerCancelled.Task;
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
