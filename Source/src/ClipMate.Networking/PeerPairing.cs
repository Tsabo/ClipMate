using System.Net;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.Internal;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

/// <summary>
/// Pairing, on both Devices: asking another Device (<see cref="Start" />) and answering one (the pairing calls of the
/// Peer service, which only carry the messages). A Device becomes a Peer only once both users have accepted, and both
/// Devices record it by the same rule.
/// </summary>
public sealed class PeerPairing : IPairingClient
{
    private const int _maxDeviceNameLength = 64;
    private readonly IPairingApprovalHandler _approvalHandler;
    private readonly ILocalDevice _localDevice;
    private readonly ILogger<PeerPairing> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPeerStore _peerStore;

    public PeerPairing(ILocalDevice localDevice, IPeerStore peerStore, IPairingApprovalHandler approvalHandler, ILoggerFactory loggerFactory)
        : this(localDevice, peerStore, approvalHandler, loggerFactory, TimeProvider.System)
    {
    }

    internal PeerPairing(ILocalDevice localDevice, IPeerStore peerStore, IPairingApprovalHandler approvalHandler, ILoggerFactory loggerFactory,
        TimeProvider timeProvider)
    {
        _localDevice = localDevice ?? throw new ArgumentNullException(nameof(localDevice));
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _approvalHandler = approvalHandler ?? throw new ArgumentNullException(nameof(approvalHandler));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<PeerPairing>();
        Requests = new PairingRequestTracker(timeProvider ?? throw new ArgumentNullException(nameof(timeProvider)));
    }

    /// <summary>
    /// Gets the requests other Devices made to this Device.
    /// </summary>
    internal PairingRequestTracker Requests { get; }

    /// <inheritdoc />
    public IPairingSession Start(PairingTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var identity = _localDevice.Identity ?? throw new InvalidOperationException("Turn on networking before pairing.");
        return new PairingSession(identity, _localDevice.DeviceName, _localDevice.ListeningPort ?? 0, target, this,
            _loggerFactory.CreateLogger<PairingSession>());
    }

    /// <summary>
    /// Answers a pairing request another Device made to this Device: asks this Device's user, and remembers an acceptance
    /// until the other Device confirms (<see cref="ConfirmAsync" />).
    /// </summary>
    /// <param name="self">The Device ID this Device listens as.</param>
    /// <param name="caller">The requesting Device's Device ID, proven by its TLS certificate.</param>
    /// <param name="remoteAddress">The IP address the request came from, if known.</param>
    /// <param name="deviceName">The Device Name the requesting Device reports.</param>
    /// <param name="protocolMajor">The requesting Device's protocol major version.</param>
    /// <param name="callerAddress">Where the requesting Device's Peer listener is (<c>host:port</c>), if known.</param>
    /// <param name="cancellationToken">Cancelled if the requesting Device gives up.</param>
    /// <returns>
    /// <see cref="PairingStatus.Accepted" />, <see cref="PairingStatus.Declined" />, <see cref="PairingStatus.UpdateRequired" />
    /// or <see cref="PairingStatus.Busy" />.
    /// </returns>
    internal async Task<PairingStatus> AnswerAsync(DeviceId self, DeviceId caller, IPAddress? remoteAddress, string deviceName, uint protocolMajor,
        string? callerAddress, CancellationToken cancellationToken)
    {
        if (caller == self)
            return PairingStatus.Declined;

        if (protocolMajor != PeerProtocol.Major)
        {
            _logger.LogInformation("Pairing request from Device {DeviceId} uses protocol {Major}; update required",
                caller.ToDisplayString(), protocolMajor);

            return PairingStatus.UpdateRequired;
        }

        if (!Requests.TryBeginWaiting(caller, remoteAddress))
            return PairingStatus.Busy;

        var accepted = false;
        try
        {
            var name = CleanDeviceName(deviceName, caller);
            var code = VerificationCode.Derive(self, caller);
            _logger.LogInformation("Pairing request from {DeviceName} ({DeviceId})", name, caller.ToDisplayString());

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PairingRequestTracker.ApprovalTimeout);

            PairingDecision decision;
            try
            {
                decision = await _approvalHandler.RequestApprovalAsync(new PairingRequest(caller, name, code), timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                decision = PairingDecision.Declined;
            }

            if (!decision.Accepted || cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Pairing request from {DeviceId} was declined", caller.ToDisplayString());
                return PairingStatus.Declined;
            }

            Requests.Accept(caller, name, decision.Alias, callerAddress);
            accepted = true;
            return PairingStatus.Accepted;
        }
        finally
        {
            Requests.EndWaiting(caller, accepted);
        }
    }

    /// <summary>
    /// Completes Pairing on this Device once the Device whose request this Device's user accepted confirms that its user
    /// accepted too.
    /// </summary>
    /// <returns><c>true</c> if <paramref name="caller" /> is now a Peer; <c>false</c> if there was no unexpired acceptance.</returns>
    internal async Task<bool> ConfirmAsync(DeviceId caller, CancellationToken cancellationToken)
    {
        if (!Requests.TryTakeAcceptance(caller, out var acceptance))
        {
            _logger.LogWarning("Ignored a pairing confirmation from Device {DeviceId} that this Device did not accept", caller.ToDisplayString());
            return false;
        }

        await RecordPairedAsync(caller, acceptance.DeviceName, acceptance.Alias, acceptance.Address, false, cancellationToken);
        return true;
    }

    /// <summary>
    /// Records a completed Pairing, on either Device.
    /// </summary>
    /// <param name="deviceId">The new Peer's Device ID.</param>
    /// <param name="deviceName">The Device Name it reported; cleaned here, so both Devices store names by one rule.</param>
    /// <param name="alias">The Peer Alias this Device's user gave it.</param>
    /// <param name="address">Where it was reached (<c>host:port</c>), if known.</param>
    /// <param name="addressEnteredManually">Whether this Device's user typed <paramref name="address" /> in; it is then pinned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal Task RecordPairedAsync(DeviceId deviceId, string? deviceName, string? alias, string? address, bool addressEnteredManually,
        CancellationToken cancellationToken) =>
        _peerStore.SavePairedAsync(deviceId, CleanDeviceName(deviceName, deviceId), alias, address, addressEnteredManually, cancellationToken);

    /// <summary>
    /// Cleans a Device Name another Device reported: drops control characters, trims, and keeps at most 64 characters.
    /// A name left blank becomes the short Device ID.
    /// </summary>
    internal static string CleanDeviceName(string? deviceName, DeviceId deviceId)
    {
        var name = new string((deviceName ?? string.Empty).Where(p => !char.IsControl(p)).ToArray()).Trim();
        if (name.Length > _maxDeviceNameLength)
            name = name[.._maxDeviceNameLength].TrimEnd();

        return name.Length == 0
            ? deviceId.ToShortDisplayString()
            : name;
    }
}
