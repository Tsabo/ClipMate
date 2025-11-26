using System.Windows;
using System.Windows.Interop;
using Brushes = System.Windows.Media.Brushes;

namespace ClipMate.App;

/// <summary>
/// Hidden window for receiving global hotkey messages.
/// This window must remain "shown" (but invisible) to receive WM_HOTKEY messages.
/// </summary>
internal class HotkeyWindow : Window
{
    public HotkeyWindow()
    {
        // Make window completely invisible and non-interactive
        Width = 0;
        Height = 0;
        Left = -10000;
        Top = -10000;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        Visibility = Visibility.Hidden;

        // Ensure we don't interfere with anything
        AllowsTransparency = true;
        Background = Brushes.Transparent;
    }

    /// <summary>
    /// Gets the window handle after it's been created.
    /// </summary>
    public IntPtr Handle
    {
        get
        {
            var helper = new WindowInteropHelper(this);
            return helper.Handle;
        }
    }
}
