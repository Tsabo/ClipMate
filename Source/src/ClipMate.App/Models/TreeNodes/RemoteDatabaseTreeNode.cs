using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.App.Models.TreeNodes;

/// <summary>
/// A top-level node for a Remote Database: a Peer's Shared Database, labelled "Database — Device". Greyed out and
/// marked "offline · last seen …" while the Peer cannot be reached.
/// <para>
/// Deliberately not a <see cref="DatabaseTreeNode" />: it has no database key, so no database configuration, backup,
/// maintenance, capture-target or editing action ever applies to it or to the nodes under it.
/// </para>
/// </summary>
public sealed class RemoteDatabaseTreeNode : TreeNodeBase
{
    private string _name;

    public RemoteDatabaseTreeNode(RemoteDatabase database, DateTime now)
    {
        Database = database ?? throw new ArgumentNullException(nameof(database));
        _name = FormatName(database, now);
        IsUnavailable = !database.IsAvailable;
    }

    /// <summary>
    /// The Remote Database this node shows.
    /// </summary>
    public RemoteDatabase Database { get; private set; }

    /// <summary>
    /// The Remote Database's address: the Peer's Device ID and the Database ID.
    /// </summary>
    public RemoteDatabaseAddress Address => Database.Address;

    /// <summary>
    /// Gets or sets whether the collection and folder tree has been fetched from the Peer.
    /// </summary>
    public bool ChildrenLoaded { get; set; }

    /// <inheritdoc />
    public override string Name => _name;

    /// <inheritdoc />
    public override string Icon => "🖥️";

    /// <inheritdoc />
    public override TreeNodeType NodeType => TreeNodeType.RemoteDatabase;

    /// <summary>
    /// Shows the latest state of the Remote Database. Its collections and folders are greyed out with it.
    /// </summary>
    public void Update(RemoteDatabase database, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(database);
        Database = database;

        var name = FormatName(database, now);
        if (name != _name)
        {
            _name = name;
            OnPropertyChanged(nameof(Name));
        }

        SetUnavailable(this, !database.IsAvailable);
    }

    /// <summary>
    /// The label: "Database — Device", plus the Peer's state when it cannot be browsed.
    /// </summary>
    public static string FormatName(RemoteDatabase database, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.Status switch
        {
            PeerConnectionStatus.Online => database.DisplayName,
            PeerConnectionStatus.UpdateRequired => $"{database.DisplayName} (update required)",
            var _ => database.LastSeen == null
                ? $"{database.DisplayName} (offline)"
                : $"{database.DisplayName} (offline · last seen {PeerItem.FormatAgo(now - database.LastSeen.Value)})",
        };
    }

    private static void SetUnavailable(TreeNodeBase node, bool unavailable)
    {
        node.IsUnavailable = unavailable;
        foreach (var item in node.Children)
            SetUnavailable(item, unavailable);
    }
}
