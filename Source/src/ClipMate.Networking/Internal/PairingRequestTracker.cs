using System.Net;
using ClipMate.Core.Models.Networking;

namespace ClipMate.Networking.Internal;

/// <summary>
/// Incoming pairing requests on the approving Device: which are waiting for this Device's user, and which this
/// user accepted and are waiting for the requesting Device to confirm. Nothing here grants trust; a Device only
/// becomes a Peer when its confirmation is matched to an acceptance and recorded in the Peer store.
/// </summary>
/// <remarks>
/// A Device ID costs nothing to mint, so requests are also limited by the address they come from: one waiting request
/// per address, and none for a short while after a request from that address was declined or went unanswered. That
/// keeps one unpaired Device on the LAN from filling the waiting slots or prompting this Device's user over and over.
/// </remarks>
internal sealed class PairingRequestTracker
{
    private const int _maxWaiting = 3;

    /// <summary>
    /// How long this Device's user has to answer a pairing request.
    /// </summary>
    public static readonly TimeSpan ApprovalTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long an acceptance waits for the requesting Device's user to accept too.
    /// </summary>
    public static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long requests from an address are refused after a request from it was declined or went unanswered.
    /// </summary>
    public static readonly TimeSpan DeclinedCoolDown = TimeSpan.FromSeconds(30);

    private readonly Dictionary<DeviceId, Acceptance> _accepted = [];
    private readonly Dictionary<IPAddress, DateTimeOffset> _coolDowns = [];
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<DeviceId, IPAddress?> _waiting = [];

    public PairingRequestTracker(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Starts waiting for the user's answer to <paramref name="deviceId" />'s request, made from
    /// <paramref name="remoteAddress" />.
    /// Returns <c>false</c> if that Device or another Device at that address already has a request waiting, a request from
    /// that address was declined moments ago, or too many requests are waiting.
    /// </summary>
    public bool TryBeginWaiting(DeviceId deviceId, IPAddress? remoteAddress)
    {
        var address = Normalize(remoteAddress);
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();
            foreach (var item in _coolDowns.Where(p => p.Value <= now).Select(p => p.Key).ToList())
                _coolDowns.Remove(item);

            if (_waiting.Count >= _maxWaiting || _waiting.ContainsKey(deviceId))
                return false;

            if (address != null && (_coolDowns.ContainsKey(address) || _waiting.ContainsValue(address)))
                return false;

            _waiting.Add(deviceId, address);

            // Only a request that actually begins replaces an earlier acceptance; a refused duplicate must not destroy it.
            _accepted.Remove(deviceId);
            return true;
        }
    }

    /// <summary>
    /// Stops waiting for the user's answer to <paramref name="deviceId" />'s request. Unless the user accepted it, requests
    /// from the same address are refused for <see cref="DeclinedCoolDown" />.
    /// </summary>
    public void EndWaiting(DeviceId deviceId, bool accepted)
    {
        lock (_lock)
        {
            if (_waiting.Remove(deviceId, out var address) && !accepted && address != null)
                _coolDowns[address] = _timeProvider.GetUtcNow() + DeclinedCoolDown;
        }
    }

    /// <summary>
    /// Records that the user accepted <paramref name="deviceId" />'s request.
    /// </summary>
    public void Accept(DeviceId deviceId, string deviceName, string? alias, string? address)
    {
        lock (_lock)
        {
            _accepted[deviceId] = new Acceptance(deviceName, alias, address, _timeProvider.GetUtcNow() + ConfirmationTimeout);
        }
    }

    /// <summary>
    /// Takes the user's unexpired acceptance of <paramref name="deviceId" />'s request, if there is one. Each acceptance can
    /// be taken once.
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

    private static IPAddress? Normalize(IPAddress? address) =>
        address is { IsIPv4MappedToIPv6: true }
            ? address.MapToIPv4()
            : address;

    /// <summary>
    /// The user's acceptance of a pairing request.
    /// </summary>
    public sealed record Acceptance(string DeviceName, string? Alias, string? Address, DateTimeOffset ExpiresAt);
}
