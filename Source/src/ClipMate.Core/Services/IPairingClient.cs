using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services;

/// <summary>
/// Asks this Device's user whether to accept a pairing request from another Device (approving side of Pairing).
/// </summary>
public interface IPairingApprovalHandler
{
    /// <summary>
    /// Shows the request (tray notification and approval dialog) and returns the user's decision.
    /// <paramref name="cancellationToken" /> is cancelled if the requesting Device gives up or the request expires;
    /// the prompt should then close and the request counts as declined.
    /// </summary>
    Task<PairingDecision> RequestApprovalAsync(PairingRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Starts Pairing with another Device (requesting side of Pairing).
/// </summary>
public interface IPairingClient
{
    /// <summary>
    /// Sends a pairing request to <paramref name="target" />. The other Device's user is asked right away;
    /// this Device's user answers through <see cref="IPairingSession.AcceptAsync" /> or <see cref="IPairingSession.Cancel" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">Networking is not enabled.</exception>
    IPairingSession Start(PairingTarget target);
}

/// <summary>
/// One outgoing pairing request. Pairing completes only once both users have accepted.
/// </summary>
public interface IPairingSession : IAsyncDisposable
{
    /// <summary>
    /// Gets the Device being asked.
    /// </summary>
    PairingTarget Target { get; }

    /// <summary>
    /// Gets the Verification Code, which the other Device shows too.
    /// </summary>
    string VerificationCode { get; }

    /// <summary>
    /// Gets the Device Name the other Device reported in its answer, or <c>null</c> until it has answered.
    /// </summary>
    string? PeerDeviceName { get; }

    /// <summary>
    /// Completes with the other Device's answer: <see cref="PairingStatus.Accepted" /> or why it did not accept.
    /// </summary>
    Task<PairingStatus> PeerResponse { get; }

    /// <summary>
    /// Accepts on this Device: waits for the other Device's answer and, if it accepted too, completes Pairing on both
    /// Devices and records the Peer with <paramref name="alias" />. Returns <see cref="PairingStatus.Paired" /> on success.
    /// </summary>
    Task<PairingStatus> AcceptAsync(string? alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Declines on this Device: withdraws the request, so neither Device becomes a Peer.
    /// </summary>
    void Cancel();
}

/// <summary>
/// This Device while networking is enabled.
/// </summary>
public interface ILocalDevice
{
    /// <summary>
    /// Gets this Device's identity while networking is enabled; otherwise <c>null</c>.
    /// </summary>
    DeviceIdentity? Identity { get; }

    /// <summary>
    /// Gets this Device's Device Name.
    /// </summary>
    string DeviceName { get; }

    /// <summary>
    /// Gets the port the Peer listener is listening on, or <c>null</c> when networking is off.
    /// </summary>
    int? ListeningPort { get; }
}
