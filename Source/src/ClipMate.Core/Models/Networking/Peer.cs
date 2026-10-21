namespace ClipMate.Core.Models.Networking;

/// <summary>
/// A Device this Device has completed Pairing with.
/// </summary>
/// <param name="DeviceId">The Peer's Device ID; only a certificate with this Device ID is accepted as the Peer.</param>
/// <param name="DeviceName">The Peer's own Device Name, as last reported by the Peer.</param>
/// <param name="Alias">The Peer Alias this Device gives the Peer, or <c>null</c>.</param>
/// <param name="Addresses">The Peer's last known addresses (<c>host:port</c>), most recent first.</param>
/// <param name="LastSeen">When this Device last reached the Peer (local time), or <c>null</c> if never.</param>
public sealed record Peer(
    DeviceId DeviceId,
    string DeviceName,
    string? Alias,
    IReadOnlyList<string> Addresses,
    DateTime? LastSeen)
{
    /// <summary>
    /// The name to show for the Peer: its Peer Alias, or its Device Name if there is no alias.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? DeviceName : Alias;

    /// <summary>
    /// When Pairing with the Peer completed on this Device (local time), or <c>null</c> if unknown.
    /// </summary>
    public DateTime? PairedAt { get; init; }
}
