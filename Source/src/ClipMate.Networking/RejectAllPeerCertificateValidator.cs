using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;

namespace ClipMate.Networking;

/// <summary>
/// Default <see cref="IPeerCertificateValidator" /> until Pairing provides the paired Device IDs: trusts no one.
/// </summary>
internal sealed class RejectAllPeerCertificateValidator : IPeerCertificateValidator
{
    /// <inheritdoc />
    public bool IsTrusted(DeviceId deviceId) => false;
}
