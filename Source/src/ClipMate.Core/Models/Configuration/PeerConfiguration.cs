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

    /// <summary>
    /// Gets or sets the Peer's Shared Databases as last reported by the Peer, so its Remote Databases can be shown
    /// (as offline) while it cannot be reached.
    /// </summary>
    public List<PeerSharedDatabaseConfiguration> SharedDatabases { get; set; } = [];

    /// <summary>
    /// Gets or sets the Remote Database Aliases this Device gives the Peer's Databases. Kept apart from
    /// <see cref="SharedDatabases" />, which the Peer's reports replace, so an alias survives the Peer renaming,
    /// unsharing or resharing its Database; it goes only when the Peer is unpaired.
    /// </summary>
    public List<PeerDatabaseAliasConfiguration> DatabaseAliases { get; set; } = [];
}

/// <summary>
/// A Remote Database Alias: the name this Device gives one of a Peer's Databases.
/// </summary>
public class PeerDatabaseAliasConfiguration
{
    /// <summary>
    /// Gets or sets the Database ID on the Peer ("D" format).
    /// </summary>
    public string DatabaseId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Remote Database Alias.
    /// </summary>
    public string Alias { get; set; } = string.Empty;
}

/// <summary>
/// A Peer's Shared Database as last reported by the Peer.
/// </summary>
public class PeerSharedDatabaseConfiguration
{
    /// <summary>
    /// Gets or sets the Database ID on the Peer ("D" format).
    /// </summary>
    public string DatabaseId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Database's name on the Peer.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
