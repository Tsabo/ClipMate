using ClipMate.App;

namespace System.Windows;

/// <summary>
/// Extension methods for WPF Window operations.
/// </summary>
public static class WindowExtensions
{
    /// <summary>
    /// Finds the best owner window for modal dialogs, or null when none can own one.
    /// Prefers the currently active ExplorerWindow or ClassicWindow, then the ExplorerWindow, then the ClassicWindow.
    /// Other windows are excluded: WPF makes whichever Window is constructed first the MainWindow, and at startup
    /// that can be a hidden utility window (e.g. HotkeyWindow), which would make a terrible, off-screen dialog owner.
    /// </summary>
    public static Window? GetDialogOwner(this Application application)
    {
        var candidates = application.Windows
            .OfType<Window>()
            .Where(p => p is ExplorerWindow or ClassicWindow && p.CanOwnDialogs())
            .ToList();

        return candidates.FirstOrDefault(p => p.IsActive)
               ?? candidates.FirstOrDefault(p => p is ExplorerWindow)
               ?? candidates.FirstOrDefault();
    }

    /// <summary>
    /// Whether a dialog can be owned by this window. WPF refuses a window that has never been shown or has been
    /// closed as an Owner, and a dialog owned by a window that is hidden or minimized to the tray opens hidden too.
    /// ClipMate starts in the tray, so the Explorer and Classic windows are often in one of those states when a
    /// dialog is opened from the tray menu or a hotkey.
    /// </summary>
    public static bool CanOwnDialogs(this Window window) =>
        window.IsVisible && window.WindowState != WindowState.Minimized;

    /// <summary>
    /// Centers a window on its owner, or on the primary monitor's work area if there is no owner.
    /// WPF's WindowStartupLocation.CenterScreen can land on the wrong monitor in multi-monitor
    /// setups when there's no owner to anchor to, so this computes the position explicitly instead.
    /// When there's no owner, also shows the window in the taskbar - without an owner to alt-tab to,
    /// an owner-less dialog (e.g. the startup backup prompt) has no other way to be found if it
    /// gets buried behind other windows.
    /// </summary>
    public static void CenterOnOwnerOrScreen(this Window window, Window? owner)
    {
        window.Owner = owner;

        if (owner != null)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return;
        }

        var workArea = SystemParameters.WorkArea;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = workArea.Left + (workArea.Width - window.Width) / 2;
        window.Top = workArea.Top + (workArea.Height - window.Height) / 2;
        window.ShowInTaskbar = true;
    }
}
