namespace ClipMate.Core.Services;

/// <summary>
/// Provides monitoring of Windows session changes (lock/unlock events).
/// </summary>
public interface ISessionMonitorService
{
    /// <summary>
    /// Starts monitoring session changes for the specified window.
    /// </summary>
    /// <param name="windowHandle">Handle of the window to receive session notifications.</param>
    void Start(nint windowHandle);

    /// <summary>
    /// Stops monitoring session changes.
    /// </summary>
    void Stop();

    /// <summary>
    /// Processes a window message to check for session change notifications.
    /// </summary>
    /// <param name="msg">The window message.</param>
    /// <param name="wParam">The wParam value.</param>
    /// <param name="lParam">The lParam value.</param>
    /// <returns>True if the message was handled; otherwise false.</returns>
    bool ProcessMessage(int msg, nint wParam, nint lParam);
}
