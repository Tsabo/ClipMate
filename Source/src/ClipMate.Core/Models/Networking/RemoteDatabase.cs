using ClipMate.Core.Services;

namespace ClipMate.Core.Models.Networking;

/// <summary>
/// The address of a Remote Database: the Peer that shares it and the Database's permanent Database ID.
/// Database IDs are only unique per Device (a copied file keeps its ID), so both parts are needed.
/// </summary>
/// <param name="DeviceId">The Device ID of the Peer that shares the Database.</param>
/// <param name="DatabaseId">The Database ID on that Peer.</param>
public readonly record struct RemoteDatabaseAddress(DeviceId DeviceId, Guid DatabaseId)
{
    /// <inheritdoc />
    public override string ToString() => $"{DeviceId.ToShortDisplayString()}/{DatabaseId:D}";
}

/// <summary>
/// A Peer's Shared Database as seen from this Device. Built at runtime from the Peer records and Peer status; it is
/// never a <see cref="Configuration.DatabaseConfiguration" /> entry, so backup, maintenance, retention and purge never see it.
/// </summary>
/// <param name="Address">Which Peer shares it, and its Database ID there.</param>
/// <param name="DatabaseName">The Database's name on the Peer.</param>
/// <param name="PeerName">The Peer Alias, or the Peer's Device Name if it has no alias.</param>
/// <param name="Status">Whether the Peer can be reached right now.</param>
/// <param name="LastSeen">When this Device last reached the Peer (local time), or <c>null</c> if never.</param>
public sealed record RemoteDatabase(
    RemoteDatabaseAddress Address,
    string DatabaseName,
    string PeerName,
    PeerConnectionStatus Status,
    DateTime? LastSeen)
{
    /// <summary>
    /// Gets whether the Peer is online, so the Remote Database can be browsed.
    /// </summary>
    public bool IsAvailable => Status == PeerConnectionStatus.Online;

    /// <summary>
    /// The label shown in the collection tree: "Database — Device".
    /// </summary>
    public string DisplayName => $"{DatabaseName} — {PeerName}";
}
