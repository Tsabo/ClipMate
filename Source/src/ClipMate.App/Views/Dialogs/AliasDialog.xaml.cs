using ClipMate.Core.Models.Networking;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Asks for a local alias: a Peer Alias or a Remote Database Alias. A blank alias clears it, so the Peer's own name is shown.
/// </summary>
public partial class AliasDialog
{
    private AliasDialog(string title, string? alias, string ownName, string toolTip, string hint)
    {
        InitializeComponent();
        Title = title;
        AliasTextEdit.EditValue = alias ?? string.Empty;
        AliasTextEdit.NullText = ownName;
        AliasTextEdit.ToolTip = toolTip;
        HintText.Text = hint;
        Loaded += (_, _) =>
        {
            AliasTextEdit.Focus();
            AliasTextEdit.SelectAll();
        };
    }

    /// <summary>
    /// Gets the alias entered; blank means no alias.
    /// </summary>
    public string Alias => AliasTextEdit.EditValue as string ?? string.Empty;

    /// <summary>
    /// Creates the dialog for a Peer's Peer Alias.
    /// </summary>
    public static AliasDialog ForPeer(Peer peer)
    {
        ArgumentNullException.ThrowIfNull(peer);
        return new AliasDialog("Rename Peer", peer.Alias, peer.DeviceName,
            "The name this Device shows for the Peer (its Peer Alias). Only you see it.",
            $"Only you see this name. Leave it blank to show the Peer's own Device Name ({peer.DeviceName}).");
    }

    /// <summary>
    /// Creates the dialog for a Remote Database's Remote Database Alias.
    /// </summary>
    public static AliasDialog ForRemoteDatabase(RemoteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return new AliasDialog("Rename Remote Database", database.Alias, database.DatabaseName,
            "The name this Device shows for the Peer's Database (its Remote Database Alias). Only you see it.",
            $"Only you see this name; {database.PeerName} keeps its own. " +
            $"Leave it blank to show the Peer's own Database name ({database.DatabaseName}).");
    }
}
