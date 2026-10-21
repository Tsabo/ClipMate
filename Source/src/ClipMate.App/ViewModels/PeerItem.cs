using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;

namespace ClipMate.App.ViewModels;

/// <summary>
/// A Peer as listed on the Devices page.
/// </summary>
/// <param name="DisplayName">The Peer Alias, or the Peer's Device Name if it has no alias.</param>
/// <param name="DeviceName">The Peer's own Device Name.</param>
/// <param name="ShortDeviceId">The short form of its Device ID.</param>
/// <param name="DeviceId">The full, grouped Device ID (shown as a tooltip).</param>
/// <param name="StatusText">Online, offline ("last seen ...") or "Update required".</param>
public sealed record PeerItem(string DisplayName, string DeviceName, string ShortDeviceId, string DeviceId, string StatusText)
{
    /// <summary>
    /// Creates the list item for <paramref name="peer" />.
    /// </summary>
    public static PeerItem From(Peer peer, PeerConnectionStatus status, DateTime now) =>
        new(peer.DisplayName, peer.DeviceName, peer.DeviceId.ToShortDisplayString(), peer.DeviceId.ToDisplayString(),
            FormatStatus(status, peer.LastSeen, now));

    /// <summary>
    /// Describes a Peer's status, e.g. "Online", "Offline (last seen 2 h ago)" or "Update required".
    /// </summary>
    public static string FormatStatus(PeerConnectionStatus status, DateTime? lastSeen, DateTime now) => status switch
    {
        PeerConnectionStatus.Online => "Online",
        PeerConnectionStatus.UpdateRequired => "Update required",
        var _ => lastSeen == null ? "Offline (never seen)" : $"Offline (last seen {FormatAgo(now - lastSeen.Value)})",
    };

    private static string FormatAgo(TimeSpan elapsed) => elapsed switch
    {
        { TotalMinutes: < 1 } => "just now",
        { TotalHours: < 1 } => $"{(int)elapsed.TotalMinutes} min ago",
        { TotalDays: < 1 } => $"{(int)elapsed.TotalHours} h ago",
        var _ => $"{(int)elapsed.TotalDays} d ago",
    };
}
