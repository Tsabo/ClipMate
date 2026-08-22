using ClipMate.App;

namespace System.Windows;

/// <summary>
/// Extension methods for WPF Window operations.
/// </summary>
public static class WindowExtensions
{
    /// <summary>
    /// Finds the best owner window for modal dialogs.
    /// Prefers the currently active ExplorerWindow or ClassicWindow, falls back to any ExplorerWindow,
    /// then Application.Current.MainWindow if it is one of those two types.
    /// MainWindow is excluded when it isn't an ExplorerWindow/ClassicWindow because WPF auto-assigns it
    /// to whichever Window is constructed first - at startup that can be a hidden utility window
    /// (e.g. HotkeyWindow), which would make a terrible, off-screen dialog owner.
    /// </summary>
    public static Window? GetDialogOwner(this Application application)
    {
        return application.Windows
                   .OfType<Window>()
                   .FirstOrDefault(p => p.IsActive && p is ExplorerWindow or ClassicWindow)
               ?? application.Windows.OfType<ExplorerWindow>().FirstOrDefault()
               ?? (application.MainWindow is ExplorerWindow or ClassicWindow
                   ? application.MainWindow
                   : null);
    }

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
