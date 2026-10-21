using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;

namespace ClipMate.Networking;

/// <summary>
/// Trusts exactly the Devices recorded as Peers in the <see cref="IPeerStore" />. Pending or declined pairing requests
/// are never recorded there, so they never grant trust.
/// </summary>
public sealed class PeerStoreCertificateValidator : IPeerCertificateValidator
{
    private readonly IPeerStore _peerStore;

    public PeerStoreCertificateValidator(IPeerStore peerStore) =>
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));

    /// <inheritdoc />
    public bool IsTrusted(DeviceId deviceId) => _peerStore.IsPaired(deviceId);
}
