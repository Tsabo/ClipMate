namespace ClipMate.Networking;

/// <summary>
/// Version of the Peer wire protocol defined in <c>Protos/peer.proto</c>.
/// </summary>
public static class PeerProtocol
{
    /// <summary>
    /// Incompatible changes bump the major version; Peers with a different major version show "update required".
    /// </summary>
    public const uint Major = 1;

    /// <summary>
    /// Backward-compatible additions bump the minor version.
    /// </summary>
    public const uint Minor = 3;

    /// <summary>
    /// The capability of serving Shared Databases (<c>Protos/shared_databases.proto</c>), added in minor version 1.
    /// </summary>
    public const string SharedDatabasesCapability = "shared-databases";

    /// <summary>
    /// The capability of serving the clips of Shared Databases (paged clip lists, formats and streamed content),
    /// added in minor version 2.
    /// </summary>
    public const string RemoteClipsCapability = "remote-clips";

    /// <summary>
    /// The capability of streaming "collection changed" notices for Shared Databases (<c>WatchChanges</c>), added in
    /// minor version 3. A Peer without it answers <c>WatchChanges</c> with UNIMPLEMENTED; its views are refreshed by polling only.
    /// </summary>
    public const string ChangeNotificationsCapability = "change-notifications";

    /// <summary>
    /// The optional features this Device supports, exchanged in Hello. New features are added here as capabilities.
    /// </summary>
    public static IReadOnlyList<string> Capabilities { get; } = ["pairing", SharedDatabasesCapability, RemoteClipsCapability, ChangeNotificationsCapability];
}
