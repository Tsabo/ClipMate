using ClipMate.Core.Models.Networking;

namespace ClipMate.App.Services;

/// <summary>
/// Asks the user about a Peer on the Devices page.
/// </summary>
public interface IPeerDialogService
{
    /// <summary>
    /// Asks for a new Peer Alias for <paramref name="peer" />. Call on the UI thread.
    /// </summary>
    /// <returns>The alias entered (blank to clear it and show the Peer's Device Name), or <c>null</c> if the user cancelled.</returns>
    string? AskPeerAlias(Peer peer);

    /// <summary>
    /// Asks the user to confirm unpairing <paramref name="peer" />. Call on the UI thread.
    /// </summary>
    bool ConfirmUnpair(Peer peer);
}
