using System.Windows;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using Application = System.Windows.Application;
using DialogResult = ClipMate.Core.Models.DialogResult;

namespace ClipMate.App.Services;

/// <summary>
/// Shows the Rename and Unpair prompts for a Peer, and the Rename prompt for a Remote Database.
/// </summary>
public sealed class PeerDialogService : IPeerDialogService
{
    private readonly IDialogService _dialogService;

    public PeerDialogService(IDialogService dialogService) =>
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

    /// <inheritdoc />
    public string? AskPeerAlias(Peer peer)
    {
        ArgumentNullException.ThrowIfNull(peer);
        var dialog = AliasDialog.ForPeer(peer);
        dialog.Owner = Application.Current.GetDialogOwner();
        return dialog.ShowDialog() == true ? dialog.Alias : null;
    }

    /// <inheritdoc />
    public string? AskRemoteDatabaseAlias(RemoteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        var dialog = AliasDialog.ForRemoteDatabase(database);
        dialog.Owner = Application.Current.GetDialogOwner();
        return dialog.ShowDialog() == true ? dialog.Alias : null;
    }

    /// <inheritdoc />
    public bool ConfirmUnpair(Peer peer)
    {
        ArgumentNullException.ThrowIfNull(peer);
        var message = $"Unpair {peer.DisplayName}?\n\n" +
                      "Both Devices stop trusting each other and any open connection drops. " +
                      "If it can't be reached now, it finds out the next time it connects. " +
                      "To pair again, both Devices must approve a new pairing request.";
        return _dialogService.ShowMessage(message, "Unpair", DialogButton.YesNo, DialogIcon.Question) == DialogResult.Yes;
    }
}
