namespace ClipMate.Core.Models.Networking;

/// <summary>
/// A pairing request another Device sent to this Device, waiting for this Device's user to accept or decline.
/// </summary>
/// <param name="DeviceId">The requesting Device's Device ID, proven by its TLS certificate.</param>
/// <param name="DeviceName">The Device Name the requesting Device reports.</param>
/// <param name="VerificationCode">The Verification Code both Devices show.</param>
public sealed record PairingRequest(DeviceId DeviceId, string DeviceName, string VerificationCode);

/// <summary>
/// This Device's user's answer to a pairing request.
/// </summary>
/// <param name="Accepted">Whether the user accepted.</param>
/// <param name="Alias">The Peer Alias from the "Show as" field, or <c>null</c> to show the Peer's Device Name.</param>
public sealed record PairingDecision(bool Accepted, string? Alias)
{
    /// <summary>
    /// The user declined (or did not answer).
    /// </summary>
    public static PairingDecision Declined { get; } = new(false, null);

    /// <summary>
    /// The user accepted, giving the Peer <paramref name="alias" />.
    /// </summary>
    public static PairingDecision Accept(string? alias) => new(true, alias);
}

/// <summary>
/// The Device this Device asks to pair with.
/// </summary>
/// <param name="DeviceId">The Device ID it must prove with its certificate.</param>
/// <param name="DeviceName">Its Device Name (e.g. from its discovery announcement), or empty if not known yet (added manually).</param>
/// <param name="Address">Where its Peer listener is (<c>host:port</c>).</param>
public sealed record PairingTarget(DeviceId DeviceId, string DeviceName, string Address);

/// <summary>
/// Where a Pairing stands, as seen by the requesting Device.
/// </summary>
public enum PairingStatus
{
    /// <summary>The other Device's user accepted; Pairing completes once this Device's user accepts too.</summary>
    Accepted,

    /// <summary>Both users accepted and both Devices are now Peers.</summary>
    Paired,

    /// <summary>The other Device's user declined (or did not answer in time).</summary>
    Declined,

    /// <summary>The other Device runs an incompatible protocol version; one of the Devices needs an update.</summary>
    UpdateRequired,

    /// <summary>The other Device is already handling other pairing requests.</summary>
    Busy,

    /// <summary>The other Device could not be reached.</summary>
    Unreachable,

    /// <summary>The request expired before both users accepted.</summary>
    TimedOut,

    /// <summary>This Device's user declined or cancelled.</summary>
    Cancelled,

    /// <summary>Pairing failed for another reason.</summary>
    Failed,

    /// <summary>
    /// The Device at the address presented a certificate that does not match the expected Device ID, so the connection was refused.
    /// </summary>
    WrongDevice,
}
