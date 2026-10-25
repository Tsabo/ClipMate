using ClipMate.App.ViewModels;
using DevExpress.Mvvm;

namespace ClipMate.App.Services;

/// <summary>
/// Creates the hotkey navigation popup next to the tray.
/// </summary>
public interface IClipPopupNotifications
{
    /// <summary>
    /// Creates, without showing it, a popup showing <paramref name="viewModel" />.
    /// </summary>
    INotification Create(HotkeyNavigationPopupViewModel viewModel);
}
