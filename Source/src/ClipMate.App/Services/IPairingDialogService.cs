using ClipMate.Core.Models.Networking;

namespace ClipMate.App.Services;

/// <summary>
/// Shows the pairing dialog on the requesting Device.
/// </summary>
public interface IPairingDialogService
{
    /// <summary>
    /// Sends a pairing request to <paramref name="target" /> and shows the dialog (Verification Code, "Show as" field)
    /// until Pairing completes, fails or the user cancels. Call on the UI thread.
    /// </summary>
    Task PairAsync(PairingTarget target);

    /// <summary>
    /// Asks for another Device's address and Device ID (<b>Add Device manually</b>) and, if the user goes ahead,
    /// pairs with it exactly as with a Discovered Device. Call on the UI thread.
    /// </summary>
    Task AddDeviceManuallyAsync();
}
