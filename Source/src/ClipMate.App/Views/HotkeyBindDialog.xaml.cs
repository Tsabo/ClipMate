using System.Windows;
using System.Windows.Input;
using ClipMate.App.Helpers;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ModifierKeys = ClipMate.Core.Models.ModifierKeys;

namespace ClipMate.App.Views;

/// <summary>
/// Dialog for capturing hotkey input.
/// </summary>
public partial class HotkeyBindDialog
{
    public HotkeyBindDialog()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public string? CapturedHotkey { get; private set; }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System
            ? e.SystemKey
            : e.Key;

        // ESC to cancel
        if (key == Key.Escape)
        {
            DialogResult = false;
            Close();
            return;
        }

        // Delete/Backspace to clear
        if (key is Key.Delete or Key.Back)
        {
            HotkeyDisplay.Text = "Press keys...";
            CapturedHotkey = null;
            OkButton.IsEnabled = false;
            return;
        }

        // Ignore modifier-only presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        // Build the hotkey
        var modifiers = ModifierKeys.None;
        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            modifiers |= ModifierKeys.Control;

        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))
            modifiers |= ModifierKeys.Alt;

        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            modifiers |= ModifierKeys.Shift;

        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows))
            modifiers |= ModifierKeys.Windows;

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        var hotkeyString = HotkeyParser.ToString(modifiers, virtualKey);

        // Display and enable OK
        HotkeyDisplay.Text = hotkeyString;
        CapturedHotkey = hotkeyString;
        OkButton.IsEnabled = true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
