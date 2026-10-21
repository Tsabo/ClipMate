using ClipMate.Core.Models.Networking;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Asks for a Peer's Peer Alias. A blank alias clears it, so the Peer's own Device Name is shown.
/// </summary>
public partial class PeerAliasDialog
{
    public PeerAliasDialog(Peer peer)
    {
        ArgumentNullException.ThrowIfNull(peer);
        InitializeComponent();
        AliasTextEdit.EditValue = peer.Alias ?? string.Empty;
        AliasTextEdit.NullText = peer.DeviceName;
        HintText.Text = $"Only you see this name. Leave it blank to show the Peer's own Device Name ({peer.DeviceName}).";
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
}
