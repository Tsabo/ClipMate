using System.Net;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using ClipMate.Networking;
using ClipMate.Networking.Protocol;
using ClipMate.Platform.Services;
using FakeItEasy;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Integration.Networking;

/// <summary>
/// Pairing between two in-process Devices over loopback mutual TLS, each with its own Peer listener,
/// Peer records and (scripted) user.
/// </summary>
[NotInParallel]
public class PairingTests
{
    private static string? _identityDirectory;
    private static DeviceIdentity? _laptopIdentity;
    private static DeviceIdentity? _desktopIdentity;
    private TestDevice _desktop = null!;
    private TestDevice _laptop = null!;

    [Before(Class)]
    public static async Task CreateIdentities()
    {
        _identityDirectory = Path.Combine(Path.GetTempPath(), $"clipmate_pairing_{Guid.NewGuid():N}");
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
    public async Task StartDevices()
    {
        _laptop = await TestDevice.StartAsync(_laptopIdentity!, "LAPTOP");
        _desktop = await TestDevice.StartAsync(_desktopIdentity!, "DESKTOP");
    }

    [After(Test)]
    public async Task StopDevices()
    {
        await _laptop.DisposeAsync();
        await _desktop.DisposeAsync();
    }

    [Test]
    public async Task Pairing_WhenBothAccept_BothBecomePeersWithTheSameVerificationCode()
    {
        // Arrange
        _desktop.User.Answer = PairingDecision.Accept("Travel laptop");
        await using var session = _laptop.PairingClient.Start(_desktop.AsTarget());

        // Act
        var status = await session.AcceptAsync("Office desktop");

        // Assert
        await Assert.That(status).IsEqualTo(PairingStatus.Paired);
        await Assert.That(_desktop.User.Requests.Single().VerificationCode).IsEqualTo(session.VerificationCode);
        await Assert.That(_desktop.User.Requests.Single().DeviceName).IsEqualTo("LAPTOP");

        var desktopOnLaptop = _laptop.Store.Find(_desktop.DeviceId);
        await Assert.That(desktopOnLaptop).IsNotNull();
        await Assert.That(desktopOnLaptop!.DisplayName).IsEqualTo("Office desktop");
        await Assert.That(desktopOnLaptop.DeviceName).IsEqualTo("DESKTOP");
        await Assert.That(desktopOnLaptop.Addresses).IsEquivalentTo([_desktop.Address]);

        var laptopOnDesktop = _desktop.Store.Find(_laptop.DeviceId);
        await Assert.That(laptopOnDesktop).IsNotNull();
        await Assert.That(laptopOnDesktop!.DisplayName).IsEqualTo("Travel laptop");
        await Assert.That(laptopOnDesktop.Addresses).IsEquivalentTo([_laptop.Address]);
    }

    [Test]
    public async Task Pairing_WhenBothAccept_PeersCanConnectBothWays()
    {
        // Arrange
        _desktop.User.Answer = PairingDecision.Accept(null);
        await using var session = _laptop.PairingClient.Start(_desktop.AsTarget());
        await session.AcceptAsync(null);

        // Act
        var laptopToDesktop = await _laptop.HelloAsync(_desktop);
        var desktopToLaptop = await _desktop.HelloAsync(_laptop);

        // Assert
        await Assert.That(laptopToDesktop.DeviceName).IsEqualTo("DESKTOP");
        await Assert.That(desktopToLaptop.DeviceName).IsEqualTo("LAPTOP");
        await Assert.That(laptopToDesktop.ProtocolMajor).IsEqualTo(PeerProtocol.Major);
        await Assert.That(laptopToDesktop.Capabilities).Contains("pairing");
    }

    [Test]
    public async Task PeerProbe_AfterPairing_ReportsThePeerAsReachable()
    {
        // Arrange
        _desktop.User.Answer = PairingDecision.Accept(null);
        await using (var session = _laptop.PairingClient.Start(_desktop.AsTarget()))
            await session.AcceptAsync(null);
        var probe = new GrpcPeerProbe(_laptop.ConfigurationService, NullLogger<GrpcPeerProbe>.Instance);

        // Act
        var reply = await probe.ProbeAsync(_laptop.Identity, _desktop.DeviceId, _desktop.Address, CancellationToken.None);
        var reverse = await probe.ProbeAsync(_desktop.Identity, _laptop.DeviceId, _laptop.Address, CancellationToken.None);

        // Assert
        await Assert.That(reply).IsNotNull();
        await Assert.That(reply!.DeviceName).IsEqualTo("DESKTOP");
        await Assert.That(reply.ProtocolMajor).IsEqualTo(PeerProtocol.Major);
        await Assert.That(reverse).IsNotNull(); // both sides recorded each other
    }

    [Test]
    public async Task Pairing_WhenTheOtherDeviceDeclines_NeitherIsPaired()
    {
        // Arrange
        _desktop.User.Answer = PairingDecision.Declined;
        await using var session = _laptop.PairingClient.Start(_desktop.AsTarget());

        // Act
        var status = await session.AcceptAsync("Office desktop");

        // Assert
        await Assert.That(status).IsEqualTo(PairingStatus.Declined);
        await Assert.That(_laptop.Store.GetPeers()).IsEmpty();
        await Assert.That(_desktop.Store.GetPeers()).IsEmpty();
        await Assert.That(async () => await _laptop.HelloAsync(_desktop)).Throws<RpcException>();
    }

    [Test]
    public async Task Pairing_WhenTheRequestingDeviceDeclinesAfterTheOtherAccepted_NeitherIsPaired()
    {
        // Arrange
        _desktop.User.Answer = PairingDecision.Accept("Travel laptop");
        await using var session = _laptop.PairingClient.Start(_desktop.AsTarget());
        await Assert.That(await session.PeerResponse).IsEqualTo(PairingStatus.Accepted);

        // Act
        session.Cancel();
        var status = await session.AcceptAsync(null);

        // Assert
        await Assert.That(status).IsEqualTo(PairingStatus.Cancelled);
        await Assert.That(_laptop.Store.GetPeers()).IsEmpty();
        await Assert.That(_desktop.Store.GetPeers()).IsEmpty();
    }

    [Test]
    public async Task Pairing_WhenTheRequestingDeviceCancelsWhileWaiting_ClosesThePromptAndNeitherIsPaired()
    {
        // Arrange: the desktop's user never answers.
        _desktop.User.Answer = null;
        await using var session = _laptop.PairingClient.Start(_desktop.AsTarget());
        await _desktop.User.Asked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Act
        session.Cancel();

        // Assert
        await Assert.That(await session.PeerResponse).IsEqualTo(PairingStatus.Cancelled);
        await _desktop.User.PromptClosed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(_desktop.Store.GetPeers()).IsEmpty();
    }

    [Test]
    public async Task UnpairedDevice_IsRefusedDuringTheTlsHandshake()
    {
        // Act / Assert
        var exception = await Assert.That(async () => await _laptop.HelloAsync(_desktop)).Throws<RpcException>();
        await Assert.That(exception!.StatusCode).IsEqualTo(StatusCode.Unavailable);
    }

    [Test]
    public async Task UnpairedDevice_OnThePairingPath_CanOnlyRequestPairing()
    {
        // Arrange
        using var channel = PeerChannelFactory.CreateForPairing(_desktop.Address, _laptop.Identity, _desktop.DeviceId);
        var client = new PeerService.PeerServiceClient(channel);

        // Act / Assert
        var hello = await Assert.That(async () => await client.HelloAsync(new HelloRequest())).Throws<RpcException>();
        await Assert.That(hello!.StatusCode).IsEqualTo(StatusCode.PermissionDenied);

        var confirm = await client.ConfirmPairingAsync(new ConfirmPairRequest());
        await Assert.That(confirm.Paired).IsFalse();
        await Assert.That(_desktop.Store.GetPeers()).IsEmpty();
    }

    [Test]
    public async Task RequestPairing_FromAnIncompatibleProtocolVersion_RepliesUpdateRequiredWithoutAskingTheUser()
    {
        // Arrange
        using var channel = PeerChannelFactory.CreateForPairing(_desktop.Address, _laptop.Identity, _desktop.DeviceId);
        var client = new PeerService.PeerServiceClient(channel);

        // Act
        var reply = await client.RequestPairingAsync(new PairRequest { DeviceName = "FUTURE", ProtocolMajor = PeerProtocol.Major + 1 });

        // Assert
        await Assert.That(reply.Result).IsEqualTo(PairResult.UpdateRequired);
        await Assert.That(_desktop.User.Requests).IsEmpty();
    }

    [Test]
    public async Task Pairing_WithTheWrongDeviceId_IsRefusedAsTheWrongDeviceAndAsksNoOne()
    {
        // Arrange: the laptop expects some other Device at the desktop's address.
        var target = _desktop.AsTarget() with { DeviceId = DeviceId.FromCertificateBytes([42]) };
        await using var session = _laptop.PairingClient.Start(target);

        // Act
        var status = await session.AcceptAsync(null);

        // Assert
        await Assert.That(status).IsEqualTo(PairingStatus.WrongDevice);
        await Assert.That(_desktop.User.Requests).IsEmpty();
        await Assert.That(_laptop.Store.GetPeers()).IsEmpty();
    }

    [Test]
    public async Task Pairing_WithNothingListeningAtTheAddress_IsUnreachable()
    {
        // Arrange: an address where no Device listens.
        int port;
        using (var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        var target = new PairingTarget(_desktop.DeviceId, string.Empty, $"127.0.0.1:{port}");
        await using var session = _laptop.PairingClient.Start(target);

        // Act
        var status = await session.AcceptAsync(null);

        // Assert
        await Assert.That(status).IsEqualTo(PairingStatus.Unreachable);
    }

    [Test]
    public async Task ManualPairing_ByHostNameAndDeviceId_RecordsTheSamePeerAndRemembersTheEnteredAddress()
    {
        // Arrange: added manually, so only the address and Device ID are known (no Device Name yet).
        _desktop.User.Answer = PairingDecision.Accept(null);
        var address = $"localhost:{_desktop.ListeningPort}";
        await using var session = _laptop.PairingClient.Start(new PairingTarget(_desktop.DeviceId, string.Empty, address));

        // Act
        var response = await session.PeerResponse;
        var status = await session.AcceptAsync(null);

        // Assert
        await Assert.That(response).IsEqualTo(PairingStatus.Accepted);
        await Assert.That(session.PeerDeviceName).IsEqualTo("DESKTOP");
        await Assert.That(status).IsEqualTo(PairingStatus.Paired);
        await Assert.That(_desktop.User.Requests.Single().VerificationCode).IsEqualTo(session.VerificationCode);

        var desktopOnLaptop = _laptop.Store.Find(_desktop.DeviceId);
        await Assert.That(desktopOnLaptop).IsNotNull();
        await Assert.That(desktopOnLaptop!.DeviceName).IsEqualTo("DESKTOP");
        await Assert.That(desktopOnLaptop.Addresses).IsEquivalentTo([address]);
        await Assert.That(_desktop.Store.IsPaired(_laptop.DeviceId)).IsTrue();

        // The remembered address reaches the Peer again.
        var probe = new GrpcPeerProbe(_laptop.ConfigurationService, NullLogger<GrpcPeerProbe>.Instance);
        var reply = await probe.ProbeAsync(_laptop.Identity, _desktop.DeviceId, desktopOnLaptop.Addresses[0], CancellationToken.None);
        await Assert.That(reply).IsNotNull();
    }

    /// <summary>
    /// The user at a test Device: answers pairing requests with <see cref="Answer" />, or never answers if it is null.
    /// </summary>
    private sealed class ScriptedUser : IPairingApprovalHandler
    {
        public PairingDecision? Answer { get; set; }

        public List<PairingRequest> Requests { get; } = [];

        public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource PromptClosed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<PairingDecision> RequestApprovalAsync(PairingRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Asked.TrySetResult();
            if (Answer != null)
                return Answer;

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                PromptClosed.TrySetResult();
            }

            return PairingDecision.Declined;
        }
    }

    private sealed class TestDevice : IAsyncDisposable, ILocalDevice
    {
        private IPeerHost _host = null!;

        private TestDevice(DeviceIdentity identity, string deviceName)
        {
            Identity = identity;
            DeviceName = deviceName;
            var configuration = new ClipMateConfiguration { Networking = { DeviceName = deviceName } };
            ConfigurationService = A.Fake<IConfigurationService>();
            A.CallTo(() => ConfigurationService.Configuration).Returns(configuration);
            Store = new PeerStore(ConfigurationService, NullLogger<PeerStore>.Instance);
            PairingClient = new PeerPairingClient(this, Store, NullLoggerFactory.Instance);
        }

        public DeviceIdentity Identity { get; }

        public DeviceId DeviceId => Identity.DeviceId;

        public string DeviceName { get; }

        public int? ListeningPort => _host.ListeningPort;

        DeviceIdentity? ILocalDevice.Identity => Identity;

        public string Address => new IPEndPoint(IPAddress.Loopback, ListeningPort!.Value).ToString();

        public IConfigurationService ConfigurationService { get; }

        public PeerStore Store { get; }

        public ScriptedUser User { get; } = new();

        public IPairingClient PairingClient { get; }

        public async ValueTask DisposeAsync() => await _host.DisposeAsync();

        public static async Task<TestDevice> StartAsync(DeviceIdentity identity, string deviceName)
        {
            var device = new TestDevice(identity, deviceName);

            // Loopback only, so the test run never triggers a Windows Firewall prompt.
            var options = new PeerHostOptions { ListenAddress = IPAddress.Loopback };
            device._host = new KestrelPeerHostFactory(new PeerStoreCertificateValidator(device.Store), device.Store, device.User,
                device.ConfigurationService, NullLoggerFactory.Instance, options).Create();
            await device._host.StartAsync(identity, 0);
            return device;
        }

        public PairingTarget AsTarget() => new(DeviceId, DeviceName, Address);

        public async Task<HelloReply> HelloAsync(TestDevice other)
        {
            using var channel = PeerChannelFactory.CreateForPeer(other.Address, Identity, other.DeviceId);
            var client = new PeerService.PeerServiceClient(channel);
            return await client.HelloAsync(new HelloRequest
            {
                ProtocolMajor = PeerProtocol.Major, ProtocolMinor = PeerProtocol.Minor, DeviceName = DeviceName,
            });
        }
    }
}
