using ClipMate.App;

namespace System.Windows;

/// <summary>
/// Extension methods for WPF Window operations.
/// </summary>
public static class WindowExtensions
{
    /// <summary>
    /// Finds the best owner window for modal dialogs.
    /// Prefers the currently active ExplorerWindow or ClassicWindow,
    /// falls back to any ExplorerWindow, then Application.Current.MainWindow.
    /// </summary>
    public static Window? GetDialogOwner(this Application application)
    {
        return application.Windows
                   .OfType<Window>()
                   .FirstOrDefault(p => p.IsActive && p is ExplorerWindow or ClassicWindow)
               ?? application.Windows.OfType<ExplorerWindow>().FirstOrDefault()
               ?? application.MainWindow;
    }
}
