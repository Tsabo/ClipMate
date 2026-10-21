using ClipMate.Core.Models.Networking;

namespace ClipMate.Networking;

/// <summary>
/// Incoming pairing requests on the approving Device: which are waiting for this Device's user, and which this
/// user accepted and are waiting for the requesting Device to confirm. Nothing here grants trust; a Device only
/// becomes a Peer when its confirmation is matched to an acceptance and recorded in the Peer store.
/// </summary>
internal sealed class PairingRequestTracker
{
    /// <summary>
    /// How long this Device's user has to answer a pairing request.
    /// </summary>
    public static readonly TimeSpan ApprovalTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long an acceptance waits for the requesting Device's user to accept too.
    /// </summary>
    public static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromMinutes(2);

    private const int _maxWaiting = 3;
    private readonly Dictionary<DeviceId, Acceptance> _accepted = [];
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<DeviceId> _waiting = [];

    public PairingRequestTracker(TimeProvider timeProvider) => _timeProvider = timeProvider;

    /// <summary>
    /// Starts waiting for the user's answer to <paramref name="deviceId" />'s request. Returns <c>false</c> if that Device
    /// already has a request waiting, or too many requests are waiting.
    /// </summary>
    public bool TryBeginWaiting(DeviceId deviceId)
    {
        lock (_lock)
        {
            if (_waiting.Count >= _maxWaiting)
                return false;

            _accepted.Remove(deviceId);
            return _waiting.Add(deviceId);
        }
    }

    /// <summary>
    /// Stops waiting for the user's answer to <paramref name="deviceId" />'s request.
    /// </summary>
    public void EndWaiting(DeviceId deviceId)
    {
        lock (_lock)
            _waiting.Remove(deviceId);
    }

    /// <summary>
    /// Records that the user accepted <paramref name="deviceId" />'s request.
    /// </summary>
    public void Accept(DeviceId deviceId, string deviceName, string? alias, string? address)
    {
        lock (_lock)
            _accepted[deviceId] = new Acceptance(deviceName, alias, address, _timeProvider.GetUtcNow() + ConfirmationTimeout);
    }

    /// <summary>
    /// Takes the user's unexpired acceptance of <paramref name="deviceId" />'s request, if there is one. Each acceptance can be taken once.
    /// </summary>
    public bool TryTakeAcceptance(DeviceId deviceId, out Acceptance acceptance)
    {
        lock (_lock)
        {
            if (_accepted.Remove(deviceId, out acceptance!) && acceptance.ExpiresAt > _timeProvider.GetUtcNow())
                return true;

            acceptance = null!;
            return false;
        }
    }

    /// <summary>
    /// The user's acceptance of a pairing request.
    /// </summary>
    public sealed record Acceptance(string DeviceName, string? Alias, string? Address, DateTimeOffset ExpiresAt);
}
