using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Ends a Pairing (Unpairing) for both Devices.
/// </summary>
public interface IUnpairingService
{
    /// <summary>
    /// Unpairs the Peer with <paramref name="deviceId" />: removes it here at once (it can no longer connect and its open
    /// connections drop), then tells the Peer if it can be reached so it unpairs too. A Peer that cannot be reached
    /// learns it was unpaired the next time the two Devices reach each other.
    /// </summary>
    /// <returns><c>true</c> if the Peer was told; <c>false</c> if it could not be reached (or networking is off).</returns>
    Task<bool> UnpairAsync(DeviceId deviceId, CancellationToken cancellationToken = default);
}
