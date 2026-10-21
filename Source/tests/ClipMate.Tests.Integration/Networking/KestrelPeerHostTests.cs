using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Networking;
using ClipMate.Networking.Protocol;
using ClipMate.Platform.Services;
using FakeItEasy;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Integration.Networking;

/// <summary>
/// Exercises the mutual-TLS gRPC host end to end over a real loopback socket.
/// Not parallel: StopAsync_ClosesTheListener would otherwise see the freed ephemeral port reused by another test's host.
/// </summary>
[NotInParallel]
public class KestrelPeerHostTests
{
    private static DeviceIdentity? _clientIdentity;
    private static DeviceIdentity? _hostIdentity;
    private static string? _identityDirectory;
    private IConfigurationService _configurationService = null!;
    private IPeerHost? _host;

    [Before(Class)]
    public static async Task CreateIdentities()
    {
        _identityDirectory = Path.Combine(Path.GetTempPath(), $"clipmate_peerhost_{Guid.NewGuid():N}");
        _hostIdentity = await new DeviceIdentityService(Path.Combine(_identityDirectory, "host"), NullLogger<DeviceIdentityService>.Instance)
            .GetOrCreateAsync();
        _clientIdentity = await new DeviceIdentityService(Path.Combine(_identityDirectory, "client"), NullLogger<DeviceIdentityService>.Instance)
            .GetOrCreateAsync();
    }

    [After(Class)]
    public static void DeleteIdentities()
    {
        _hostIdentity?.Dispose();
        _clientIdentity?.Dispose();
        if (_identityDirectory != null && Directory.Exists(_identityDirectory))
            Directory.Delete(_identityDirectory, true);
    }

    [Before(Test)]
    public void Setup()
    {
        _configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => _configurationService.Configuration)
            .Returns(new ClipMateConfiguration { Networking = { DeviceName = "Host Device" } });
    }

    [After(Test)]
    public async Task Cleanup()
    {
        if (_host != null)
            await _host.DisposeAsync();
    }

    [Test]
    public async Task Hello_FromTrustedDevice_ReturnsHostIdentityAndProtocolVersion()
    {
        // Arrange
        var port = await StartHostAsync(new AllowListValidator(_clientIdentity!.DeviceId));
        using var channel = PeerChannelFactory.Create(new Uri($"https://localhost:{port}"), _clientIdentity, _hostIdentity!.DeviceId);
        var client = new PeerService.PeerServiceClient(channel);

        // Act
        var reply = await client.HelloAsync(new HelloRequest { ProtocolMajor = PeerProtocol.Major, ProtocolMinor = PeerProtocol.Minor });

        // Assert
        await Assert.That(reply.DeviceId).IsEqualTo(_hostIdentity.DeviceId.Value);
        await Assert.That(reply.DeviceName).IsEqualTo("Host Device");
        await Assert.That(reply.ProtocolMajor).IsEqualTo(PeerProtocol.Major);
    }

    [Test]
    public async Task Hello_FromUntrustedDevice_IsRejected()
    {
        // Arrange
        var port = await StartHostAsync(new AllowListValidator());
        using var channel = PeerChannelFactory.Create(new Uri($"https://localhost:{port}"), _clientIdentity!, _hostIdentity!.DeviceId);
        var client = new PeerService.PeerServiceClient(channel);

        // Act / Assert
        await Assert.That(async () => await client.HelloAsync(new HelloRequest())).Throws<RpcException>();
    }

    [Test]
    public async Task Hello_WhenHostIsNotTheExpectedDevice_IsRejectedByClient()
    {
        // Arrange: the client expects to reach itself, so the host's certificate does not match.
        var port = await StartHostAsync(new AllowListValidator(_clientIdentity!.DeviceId));
        using var channel = PeerChannelFactory.Create(new Uri($"https://localhost:{port}"), _clientIdentity, _clientIdentity.DeviceId);
        var client = new PeerService.PeerServiceClient(channel);

        // Act / Assert
        await Assert.That(async () => await client.HelloAsync(new HelloRequest())).Throws<RpcException>();
    }

    [Test]
    public async Task StopAsync_ClosesTheListener()
    {
        // Arrange
        var port = await StartHostAsync(new AllowListValidator(_clientIdentity!.DeviceId));

        // Act
        await _host!.StopAsync();

        // Assert
        await Assert.That(_host.IsRunning).IsFalse();
        await Assert.That(_host.ListeningPort).IsNull();
        using var tcp = new System.Net.Sockets.TcpClient();
        await Assert.That(async () => await tcp.ConnectAsync(System.Net.IPAddress.Loopback, port)).Throws<System.Net.Sockets.SocketException>();
    }

    private async Task<int> StartHostAsync(IPeerCertificateValidator validator)
    {
        // Loopback only, so the test run never triggers a Windows Firewall prompt.
        var options = new PeerHostOptions { ListenAddress = System.Net.IPAddress.Loopback };
        _host = new KestrelPeerHostFactory(validator, A.Fake<IPeerStore>(), A.Fake<IPairingApprovalHandler>(), _configurationService, A.Fake<ISharedDatabaseService>(),
            NullLoggerFactory.Instance, options).Create();
        await _host.StartAsync(_hostIdentity!, 0);
        await Assert.That(_host.IsRunning).IsTrue();
        return _host.ListeningPort!.Value;
    }

    private sealed class AllowListValidator(params DeviceId[] allowed) : IPeerCertificateValidator
    {
        public bool IsTrusted(DeviceId deviceId) => allowed.Contains(deviceId);
    }
}
