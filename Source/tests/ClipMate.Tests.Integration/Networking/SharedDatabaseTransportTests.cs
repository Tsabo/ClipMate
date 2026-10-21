using System.Net;
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

        var discovery = A.Fake<IDeviceDiscoveryService>();
        A.CallTo(() => discovery.DiscoveredDevices).Returns([]);
        _client = new GrpcSharedDatabaseClient(discovery, NullLogger<GrpcSharedDatabaseClient>.Instance);
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
