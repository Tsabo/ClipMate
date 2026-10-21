namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// A Peer record as stored in the <c>peers</c> section of clipmate.toml. Read and changed only through
/// <see cref="Services.IPeerStore" />. Peers never expire; a record stays until the Peer is unpaired.
/// </summary>
public class PeerConfiguration
{
    /// <summary>
    /// Gets or sets the Peer's Device ID in canonical form.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Peer's own Device Name as last reported by the Peer.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Peer Alias this Device gives the Peer, or <c>null</c> to show its Device Name.
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    /// Gets or sets the Peer's last known addresses (<c>host:port</c>), most recent first.
    /// </summary>
    public List<string> Addresses { get; set; } = [];

    /// <summary>
    /// Gets or sets when this Device last reached the Peer (local time), or <c>null</c> if never.
    /// </summary>
    public DateTime? LastSeen { get; set; }

    /// <summary>
    /// Gets or sets when Pairing with the Peer completed (local time).
    /// </summary>
    public DateTime PairedAt { get; set; }
}
