using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using ClipMate.Networking.SharedDatabases;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Networking;

public class RemoteDatabaseServiceTests
{
    private static readonly DeviceId _laptop = DeviceId.FromCertificateBytes([1]);
    private static readonly SharedDatabase _work = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Work");
    private static readonly SharedDatabase _home = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Home");
    private ISharedDatabaseClient _client = null!;
    private ClipMateConfiguration _configuration = null!;
    private DeviceIdentity _identity = null!;
    private RemoteDatabaseService _service = null!;
    private IPeerStatusService _status = null!;
    private InMemoryPeerStore _store = null!;

    [Before(Test)]
    public void Setup()
    {
        _configuration = new ClipMateConfiguration { Networking = { Enabled = true } };
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);

        _identity = new DeviceIdentity(CreateCertificate());
        var localDevice = A.Fake<ILocalDevice>();
        A.CallTo(() => localDevice.Identity).Returns(_identity);

        _store = new InMemoryPeerStore();
        _store.Add(new Peer(_laptop, "LAPTOP", null, ["192.168.1.20:47823"], new DateTime(2026, 10, 20, 9, 0, 0)));
        _status = A.Fake<IPeerStatusService>();
        _client = A.Fake<ISharedDatabaseClient>();
        _service = new RemoteDatabaseService(_store, _status, localDevice, configurationService, _client, new NoTimers(),
            NullLogger<RemoteDatabaseService>.Instance);
    }

    [After(Test)]
    public void Cleanup()
    {
        _service.Dispose();
        _identity.Dispose();
    }

    [Test]
    public async Task RefreshAsync_OnlinePeer_ListsEachSharedDatabaseAsAnAvailableRemoteDatabase()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work, _home);

        // Act
        await _service.RefreshAsync();

        // Assert
        var remote = _service.GetRemoteDatabases();
        await Assert.That(remote.Select(p => p.DisplayName)).IsEquivalentTo(["Home — LAPTOP", "Work — LAPTOP"]);
        await Assert.That(remote.All(p => p.IsAvailable)).IsTrue();
        await Assert.That(remote.Select(p => p.Address)).Contains(new RemoteDatabaseAddress(_laptop, _work.DatabaseId));
    }

    [Test]
    public async Task GetRemoteDatabases_PeerWithAlias_LabelsWithTheAlias()
    {
        // Arrange
        _store.Replace(_store.Find(_laptop)! with { Alias = "Travel laptop" });
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);

        // Act
        await _service.RefreshAsync();

        // Assert
        await Assert.That(_service.GetRemoteDatabases().Single().DisplayName).IsEqualTo("Work — Travel laptop");
    }

    [Test]
    public async Task SetAliasAsync_LabelsTheRemoteDatabaseWithItsAliasAndRaisesRemoteDatabasesChanged()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work, _home);
        await _service.RefreshAsync();
        var raised = 0;
        _service.RemoteDatabasesChanged += (_, _) => raised++;

        // Act
        await _service.SetAliasAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), "Office clips");

        // Assert
        var work = _service.GetRemoteDatabases().Single(p => p.Address.DatabaseId == _work.DatabaseId);
        await Assert.That(work.DisplayName).IsEqualTo("Office clips — LAPTOP");
        await Assert.That(work.DatabaseName).IsEqualTo("Work");
        await Assert.That(work.Alias).IsEqualTo("Office clips");
        await Assert.That(_service.GetRemoteDatabases().Single(p => p.Address.DatabaseId == _home.DatabaseId).DisplayName)
            .IsEqualTo("Home — LAPTOP");
        await Assert.That(raised).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task SetAliasAsync_PeerAliasSet_UsesBothAliases()
    {
        // Arrange
        _store.Replace(_store.Find(_laptop)! with { Alias = "Travel laptop" });
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);
        await _service.RefreshAsync();

        // Act
        await _service.SetAliasAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), "Office clips");

        // Assert
        await Assert.That(_service.GetRemoteDatabases().Single().DisplayName).IsEqualTo("Office clips — Travel laptop");
    }

    [Test]
    public async Task RefreshAsync_PeerRenamesAnAliasedDatabase_KeepsTheAlias()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);
        await _service.RefreshAsync();
        await _service.SetAliasAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), "Office clips");
        SharesOnLaptop(_work with { Name = "Work 2026" });

        // Act
        await _service.RefreshAsync();

        // Assert
        var remote = _service.GetRemoteDatabases().Single();
        await Assert.That(remote.DisplayName).IsEqualTo("Office clips — LAPTOP");
        await Assert.That(remote.DatabaseName).IsEqualTo("Work 2026");
    }

    [Test]
    public async Task SetAliasAsync_Blank_ShowsThePeersCurrentDatabaseName()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);
        await _service.RefreshAsync();
        var address = new RemoteDatabaseAddress(_laptop, _work.DatabaseId);
        await _service.SetAliasAsync(address, "Office clips");
        SharesOnLaptop(_work with { Name = "Work 2026" });
        await _service.RefreshAsync();

        // Act
        await _service.SetAliasAsync(address, "");

        // Assert
        var remote = _service.GetRemoteDatabases().Single();
        await Assert.That(remote.DisplayName).IsEqualTo("Work 2026 — LAPTOP");
        await Assert.That(remote.Alias).IsNull();
    }

    [Test]
    public async Task GetRemoteDatabases_PeerWithAnAliasedDatabaseGoesOfflineAndBack_KeepsTheAlias()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);
        await _service.RefreshAsync();
        await _service.SetAliasAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), "Office clips");

        // Act / Assert
        SetStatus(PeerConnectionStatus.Offline);
        await _service.RefreshAsync();
        await Assert.That(_service.GetRemoteDatabases().Single().DisplayName).IsEqualTo("Office clips — LAPTOP");

        SetStatus(PeerConnectionStatus.Online);
        await _service.RefreshAsync();
        await Assert.That(_service.GetRemoteDatabases().Single().DisplayName).IsEqualTo("Office clips — LAPTOP");
    }

    [Test]
    public async Task GetRemoteDatabases_PeerOffline_KeepsItsLastKnownDatabasesAsUnavailableWithoutContactingIt()
    {
        // Arrange: shared while online, then the laptop went away.
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);
        await _service.RefreshAsync();
        SetStatus(PeerConnectionStatus.Offline);
        Fake.ClearRecordedCalls(_client);

        // Act
        await _service.RefreshAsync();
        var remote = _service.GetRemoteDatabases().Single();

        // Assert
        await Assert.That(remote.IsAvailable).IsFalse();
        await Assert.That(remote.Status).IsEqualTo(PeerConnectionStatus.Offline);
        await Assert.That(remote.LastSeen).IsEqualTo(new DateTime(2026, 10, 20, 9, 0, 0));
        A.CallTo(_client).MustNotHaveHappened();
    }

    [Test]
    public async Task RefreshAsync_PeerStopsSharingADatabase_RemovesItAndRaisesRemoteDatabasesChanged()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work, _home);
        await _service.RefreshAsync();
        var raised = 0;
        _service.RemoteDatabasesChanged += (_, _) => raised++;
        SharesOnLaptop(_home);

        // Act
        await _service.RefreshAsync();

        // Assert
        await Assert.That(_service.GetRemoteDatabases().Select(p => p.DatabaseName)).IsEquivalentTo(["Home"]);
        await Assert.That(raised).IsEqualTo(1);
    }

    [Test]
    public async Task RefreshAsync_NothingChanged_DoesNotRaiseRemoteDatabasesChanged()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);
        await _service.RefreshAsync();
        var raised = 0;
        _service.RemoteDatabasesChanged += (_, _) => raised++;

        // Act
        await _service.RefreshAsync();

        // Assert
        await Assert.That(raised).IsEqualTo(0);
    }

    [Test]
    public async Task PeersChanged_PeerUnpaired_RemovesItsRemoteDatabasesAndRaisesRemoteDatabasesChanged()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);
        await _service.RefreshAsync();
        var raised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.RemoteDatabasesChanged += (_, _) => raised.TrySetResult();

        // Act
        _store.Remove(_laptop);
        await raised.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(_service.GetRemoteDatabases()).IsEmpty();
    }

    [Test]
    public async Task PeerStoreRemoveAsync_Unpairing_RemovesThePeersRemoteDatabases()
    {
        // Arrange: the real Peer store, as unpairing (IUnpairingService) uses it.
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);
        var store = new ClipMate.Data.Services.PeerStore(configurationService, NullLogger<ClipMate.Data.Services.PeerStore>.Instance);
        await store.SavePairedAsync(_laptop, "LAPTOP", null, null);
        await store.RecordSharedDatabasesAsync(_laptop, [_work]);
        var localDevice = A.Fake<ILocalDevice>();
        A.CallTo(() => localDevice.Identity).Returns(_identity);
        using var service = new RemoteDatabaseService(store, _status, localDevice, configurationService, _client, new NoTimers(),
            NullLogger<RemoteDatabaseService>.Instance);
        await Assert.That(service.GetRemoteDatabases()).IsNotEmpty();
        var raised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.RemoteDatabasesChanged += (_, _) => raised.TrySetResult();

        // Act
        await store.RemoveAsync(_laptop);
        await raised.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(service.GetRemoteDatabases()).IsEmpty();
    }

    [Test]
    public async Task StatusChanged_PeerComesOnline_FetchesItsSharedDatabases()
    {
        // Arrange
        SharesOnLaptop(_work);
        var raised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.RemoteDatabasesChanged += (_, _) => raised.TrySetResult();

        // Act
        SetStatus(PeerConnectionStatus.Online);
        _status.StatusChanged += Raise.WithEmpty();
        await raised.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(_service.GetRemoteDatabases().Single().Address).IsEqualTo(new RemoteDatabaseAddress(_laptop, _work.DatabaseId));
    }

    [Test]
    public async Task GetRemoteDatabases_NetworkingOff_IsEmpty()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);
        await _service.RefreshAsync();

        // Act
        _configuration.Networking.Enabled = false;
        await _service.RefreshAsync();

        // Assert
        await Assert.That(_service.GetRemoteDatabases()).IsEmpty();
    }

    [Test]
    public async Task GetRemoteDatabases_NeverAppearInTheDatabaseConfiguration()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        SharesOnLaptop(_work);

        // Act
        await _service.RefreshAsync();

        // Assert
        await Assert.That(_service.GetRemoteDatabases()).IsNotEmpty();
        await Assert.That(_configuration.Databases).IsEmpty();
    }

    [Test]
    public async Task GetCollectionTreeAsync_OnlinePeer_ReturnsTheTreeFromThePeer()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        var tree = new[] { new SharedCollection(Guid.NewGuid(), null, "Inbox", SharedCollectionKind.Collection, 10) };
        A.CallTo(() => _client.GetCollectionTreeAsync(_identity, A<Peer>.That.Matches(p => p.DeviceId == _laptop), _work.DatabaseId, A<CancellationToken>._))
            .Returns(tree);

        // Act
        var result = await _service.GetCollectionTreeAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId));

        // Assert
        await Assert.That(result).IsEquivalentTo(tree);
    }

    [Test]
    public async Task GetCollectionTreeAsync_PeerOffline_ThrowsWithoutContactingIt()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Offline);

        // Act / Assert
        await Assert.That(async () => await _service.GetCollectionTreeAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId)))
            .Throws<RemoteDatabaseUnavailableException>();
        A.CallTo(_client).MustNotHaveHappened();
    }

    [Test]
    public async Task GetCollectionTreeAsync_DatabaseNoLongerShared_Throws()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        A.CallTo(() => _client.GetCollectionTreeAsync(A<DeviceIdentity>._, A<Peer>._, A<Guid>._, A<CancellationToken>._))
            .Returns((IReadOnlyList<SharedCollection>?)null);

        // Act / Assert
        await Assert.That(async () => await _service.GetCollectionTreeAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId)))
            .Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task GetClipPageAsync_OnlinePeer_ReturnsThePageFromThePeer()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        var collectionId = Guid.NewGuid();
        var page = new ClipPage([new Clip { Id = Guid.NewGuid() }], 200, 4212);
        A.CallTo(() => _client.GetClipPageAsync(_identity, A<Peer>.That.Matches(p => p.DeviceId == _laptop), _work.DatabaseId, collectionId, null, 200, 200,
            A<CancellationToken>._)).Returns(page);

        // Act
        var result = await _service.GetClipPageAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), collectionId, null, 200, 200);

        // Assert
        await Assert.That(result).IsSameReferenceAs(page);
    }

    [Test]
    public async Task GetClipPageAsync_PeerOffline_ThrowsWithoutContactingIt()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Offline);

        // Act / Assert
        await Assert.That(async () => await _service.GetClipPageAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), Guid.NewGuid(), null, 0, 200))
            .Throws<RemoteDatabaseUnavailableException>();
        A.CallTo(_client).MustNotHaveHappened();
    }

    [Test]
    public async Task GetClipPageAsync_CollectionNoLongerShared_Throws()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        A.CallTo(() => _client.GetClipPageAsync(A<DeviceIdentity>._, A<Peer>._, A<Guid>._, A<Guid>._, A<Guid?>._, A<int>._, A<int>._, A<CancellationToken>._))
            .Returns((ClipPage?)null);

        // Act / Assert
        await Assert.That(async () => await _service.GetClipPageAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), Guid.NewGuid(), null, 0, 200))
            .Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task SearchAsync_OnlinePeer_ReturnsTheMatchesFromThePeer()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        var filters = new SearchFilters { TitleQuery = "report" };
        var matches = new SearchMatches([new Clip { Id = Guid.NewGuid() }], 4212);
        A.CallTo(() => _client.SearchAsync(_identity, A<Peer>.That.Matches(p => p.DeviceId == _laptop), _work.DatabaseId, filters, A<CancellationToken>._))
            .Returns(matches);

        // Act
        var result = await _service.SearchAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), filters);

        // Assert
        await Assert.That(result).IsSameReferenceAs(matches);
    }

    [Test]
    public async Task SearchAsync_PeerOffline_ThrowsWithoutContactingIt()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Offline);

        // Act / Assert
        await Assert.That(async () => await _service.SearchAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), new SearchFilters()))
            .Throws<RemoteDatabaseUnavailableException>();
        A.CallTo(_client).MustNotHaveHappened();
    }

    [Test]
    public async Task SearchAsync_NoLongerShared_Throws()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        A.CallTo(() => _client.SearchAsync(A<DeviceIdentity>._, A<Peer>._, A<Guid>._, A<SearchFilters>._, A<CancellationToken>._))
            .Returns((SearchMatches?)null);

        // Act / Assert
        await Assert.That(async () => await _service.SearchAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), new SearchFilters()))
            .Throws<RemoteDatabaseUnavailableException>();
    }

    [Test]
    public async Task GetClipContentAsync_NetworkingOff_ThrowsWithoutContactingThePeer()
    {
        // Arrange
        SetStatus(PeerConnectionStatus.Online);
        _configuration.Networking.Enabled = false;

        // Act / Assert
        await Assert.That(async () => await _service.GetClipContentAsync(new RemoteDatabaseAddress(_laptop, _work.DatabaseId), Guid.NewGuid()))
            .Throws<RemoteDatabaseUnavailableException>();
        A.CallTo(_client).MustNotHaveHappened();
    }

    [Test]
    public async Task StatusChanged_PeerComesOnline_WatchesItAndReportsWhichCollectionChanged()
    {
        // Arrange
        SharesOnLaptop(_work);
        var watch = WatchesOnLaptop();
        var collectionId = Guid.NewGuid();
        var changed = new TaskCompletionSource<RemoteCollectionChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.RemoteCollectionChanged += (_, e) => changed.TrySetResult(e);
        await BringLaptopOnlineAsync();

        // Act
        await watch.Started.WaitAsync(TimeSpan.FromSeconds(5));
        watch.Notices.Writer.TryWrite(new CollectionChangedNotice(new SharedCollectionChange(_work.DatabaseId, collectionId)));

        // Assert
        var change = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(change.Address).IsEqualTo(new RemoteDatabaseAddress(_laptop, _work.DatabaseId));
        await Assert.That(change.CollectionId).IsEqualTo(collectionId);
    }

    [Test]
    public async Task CollectionChangedNotice_ForADatabaseThePeerDoesNotShare_IsIgnored()
    {
        // Arrange
        SharesOnLaptop(_work);
        var watch = WatchesOnLaptop();
        var changes = new List<RemoteCollectionChangedEventArgs>();
        var last = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var collectionId = Guid.NewGuid();
        _service.RemoteCollectionChanged += (_, e) =>
        {
            lock (changes)
                changes.Add(e);

            if (e.CollectionId == collectionId)
                last.TrySetResult();
        };
        await BringLaptopOnlineAsync();
        await watch.Started.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        watch.Notices.Writer.TryWrite(new CollectionChangedNotice(new SharedCollectionChange(_home.DatabaseId, Guid.NewGuid())));
        watch.Notices.Writer.TryWrite(new CollectionChangedNotice(new SharedCollectionChange(_work.DatabaseId, collectionId)));
        await last.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        List<Guid> received;
        lock (changes)
            received = changes.Select(p => p.Address.DatabaseId).ToList();

        await Assert.That(received).IsEquivalentTo([_work.DatabaseId]);
    }

    [Test]
    public async Task SharedDatabasesChangedNotice_PeerUnsharesADatabase_RemovesItWithoutWaitingForThePoll()
    {
        // Arrange
        SharesOnLaptop(_work, _home);
        var watch = WatchesOnLaptop();
        await BringLaptopOnlineAsync();
        await watch.Started.WaitAsync(TimeSpan.FromSeconds(5));
        var raised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.RemoteDatabasesChanged += (_, _) => raised.TrySetResult();
        SharesOnLaptop(_home);

        // Act
        watch.Notices.Writer.TryWrite(new SharedDatabasesChangedNotice());
        await raised.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(_service.GetRemoteDatabases().Select(p => p.DatabaseName)).IsEquivalentTo(["Home"]);
    }

    [Test]
    public async Task PeerUnpaired_StopsWatchingIt()
    {
        // Arrange
        SharesOnLaptop(_work);
        var watch = WatchesOnLaptop();
        await BringLaptopOnlineAsync();
        var token = await watch.Started.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        _store.Remove(_laptop);

        // Assert
        await WaitUntilAsync(() => token.IsCancellationRequested);
        await Assert.That(token.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task NetworkingTurnedOff_StopsWatching()
    {
        // Arrange
        SharesOnLaptop(_work);
        var watch = WatchesOnLaptop();
        await BringLaptopOnlineAsync();
        var token = await watch.Started.WaitAsync(TimeSpan.FromSeconds(5));

        // Act: turning networking off stops the status monitor, which forgets every status
        _configuration.Networking.Enabled = false;
        SetStatus(PeerConnectionStatus.Unknown);
        _status.StatusChanged += Raise.WithEmpty();

        // Assert
        await WaitUntilAsync(() => token.IsCancellationRequested);
        await Assert.That(token.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task PeerGoesOfflineAndComesBack_StopsWatchingThenWatchesAgain()
    {
        // Arrange
        SharesOnLaptop(_work);
        var watch = WatchesOnLaptop();
        await BringLaptopOnlineAsync();
        var first = await watch.Started.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        SetStatus(PeerConnectionStatus.Offline);
        _status.StatusChanged += Raise.WithEmpty();
        await WaitUntilAsync(() => first.IsCancellationRequested);
        var again = watch.Restart();
        SetStatus(PeerConnectionStatus.Online);
        _status.StatusChanged += Raise.WithEmpty();

        // Assert
        var second = await again.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(first.IsCancellationRequested).IsTrue();
        await Assert.That(second.IsCancellationRequested).IsFalse();
    }

    [Test]
    public async Task PeerWithoutChangeNotifications_KeepsWorkingAndIsNotAskedAgainWhileOnline()
    {
        // Arrange
        SharesOnLaptop(_work);
        var asked = 0;
        A.CallTo(() => _client.WatchChangesAsync(A<DeviceIdentity>._, A<Peer>.That.Matches(p => p.DeviceId == _laptop), A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                Interlocked.Increment(ref asked);
                return ThrowsAsync(new NotSupportedException("Too old."));
            });
        await BringLaptopOnlineAsync();
        await WaitUntilAsync(() => Volatile.Read(ref asked) == 1);

        // Act
        _status.StatusChanged += Raise.WithEmpty();
        await _service.RefreshAsync();
        await Task.Delay(100);

        // Assert
        await Assert.That(Volatile.Read(ref asked)).IsEqualTo(1);
        await Assert.That(_service.GetRemoteDatabases().Single().IsAvailable).IsTrue();
    }

    private async Task BringLaptopOnlineAsync()
    {
        var raised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs e) => raised.TrySetResult();
        _service.RemoteDatabasesChanged += OnChanged;
        SetStatus(PeerConnectionStatus.Online);
        _status.StatusChanged += Raise.WithEmpty();
        await raised.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _service.RemoteDatabasesChanged -= OnChanged;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    private static async IAsyncEnumerable<PeerChangeNotice> ThrowsAsync(Exception exception)
    {
        await Task.Yield();
        if (exception != null)
            throw exception;

        yield break;
    }

    private WatchStream WatchesOnLaptop()
    {
        var watch = new WatchStream();
        A.CallTo(() => _client.WatchChangesAsync(A<DeviceIdentity>._, A<Peer>.That.Matches(p => p.DeviceId == _laptop), A<CancellationToken>._))
            .ReturnsLazily((DeviceIdentity _, Peer _, CancellationToken token) => watch.Open(token));
        return watch;
    }

    /// <summary>
    /// A Peer's change stream that the test writes to.
    /// </summary>
    private sealed class WatchStream
    {
        private TaskCompletionSource<CancellationToken> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Channel<PeerChangeNotice> Notices { get; } = Channel.CreateUnbounded<PeerChangeNotice>();

        /// <summary>
        /// Completes with the token of the latest watch when it starts.
        /// </summary>
        public Task<CancellationToken> Started => _started.Task;

        public Task<CancellationToken> Restart()
        {
            _started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _started.Task;
        }

        public IAsyncEnumerable<PeerChangeNotice> Open(CancellationToken token)
        {
            _started.TrySetResult(token);
            return Notices.Reader.ReadAllAsync(token);
        }
    }

    private void SetStatus(PeerConnectionStatus status) => A.CallTo(() => _status.GetStatus(_laptop)).Returns(status);

    private void SharesOnLaptop(params SharedDatabase[] databases) =>
        A.CallTo(() => _client.ListSharedDatabasesAsync(A<DeviceIdentity>._, A<Peer>.That.Matches(p => p.DeviceId == _laptop), A<CancellationToken>._))
            .Returns(databases);

    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=ClipMate Test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    /// <summary>
    /// Timers that never fire, so only the test decides when to refresh.
    /// </summary>
    private sealed class NoTimers : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NullTimer();

        private sealed class NullTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A Peer store that can also unpair (ticket 07 adds that to the real store).
    /// </summary>
    private sealed class InMemoryPeerStore : IPeerStore
    {
        private readonly Lock _lock = new();
        private List<Peer> _peers = [];

        public event EventHandler? PeersChanged;

        public event EventHandler<PeerRemovedEventArgs>? PeerRemoved;

        public Task SetAliasAsync(DeviceId deviceId, string? alias, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetDatabaseAliasAsync(DeviceId deviceId, Guid databaseId, string? alias, CancellationToken cancellationToken = default)
        {
            if (Find(deviceId) is { } peer)
            {
                var aliases = peer.DatabaseAliases.Where(p => p.Key != databaseId).ToDictionary();
                if (!string.IsNullOrWhiteSpace(alias))
                    aliases[databaseId] = alias;

                Replace(peer with { DatabaseAliases = aliases });
            }

            PeersChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
        {
            var paired = IsPaired(deviceId);
            Remove(deviceId);
            return Task.FromResult(paired);
        }

        public IReadOnlyList<Peer> GetPeers()
        {
            lock (_lock)
                return _peers.ToList();
        }

        public Peer? Find(DeviceId deviceId) => GetPeers().FirstOrDefault(p => p.DeviceId == deviceId);

        public bool IsPaired(DeviceId deviceId) => Find(deviceId) != null;

        public Task SavePairedAsync(DeviceId deviceId, string deviceName, string? alias, string? address, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SavePairedAsync(DeviceId deviceId, string deviceName, string? alias, string? address, bool pinAddress,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordSeenAsync(DeviceId deviceId, string deviceName, string? address, DateTime seenAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordSharedDatabasesAsync(DeviceId deviceId, IReadOnlyList<SharedDatabase> sharedDatabases, CancellationToken cancellationToken = default)
        {
            if (Find(deviceId) is { } peer)
                Replace(peer with { SharedDatabases = sharedDatabases });

            return Task.CompletedTask;
        }

        public void Add(Peer peer)
        {
            lock (_lock)
                _peers = [.. _peers, peer];
        }

        public void Replace(Peer peer)
        {
            lock (_lock)
                _peers = [.. _peers.Where(p => p.DeviceId != peer.DeviceId), peer];
        }

        public void Remove(DeviceId deviceId)
        {
            lock (_lock)
                _peers = [.. _peers.Where(p => p.DeviceId != deviceId)];

            PeerRemoved?.Invoke(this, new PeerRemovedEventArgs(deviceId));
            PeersChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
