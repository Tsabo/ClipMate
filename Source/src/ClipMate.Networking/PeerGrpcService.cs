using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Networking.Protocol;
using Grpc.Core;

namespace ClipMate.Networking;

/// <summary>
/// Serves the Peer protocol. Only Devices accepted by the <see cref="IPeerCertificateValidator" /> reach it,
/// because untrusted client certificates are rejected during the TLS handshake.
/// </summary>
internal sealed class PeerGrpcService : PeerService.PeerServiceBase
{
    private readonly IConfigurationService _configurationService;
    private readonly DeviceIdentity _identity;

    public PeerGrpcService(DeviceIdentity identity, IConfigurationService configurationService)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    /// <inheritdoc />
    public override Task<HelloReply> Hello(HelloRequest request, ServerCallContext context) =>
        Task.FromResult(new HelloReply
        {
            DeviceId = _identity.DeviceId.Value,
            DeviceName = _configurationService.Configuration.Networking.DeviceName,
            ProtocolMajor = PeerProtocol.Major,
            ProtocolMinor = PeerProtocol.Minor,
        });
}
