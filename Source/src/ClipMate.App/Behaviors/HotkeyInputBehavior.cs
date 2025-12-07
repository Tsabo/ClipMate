using System.Windows;
using System.Windows.Input;
using ClipMate.App.Helpers;
using DevExpress.Xpf.Editors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ModifierKeys = ClipMate.Core.Models.ModifierKeys;

namespace ClipMate.App.Behaviors;

/// <summary>
/// Attached behavior for capturing hotkey input in a TextEdit control.
/// </summary>
public static class HotkeyInputBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(HotkeyInputBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEdit textEdit)
            return;

        if ((bool)e.NewValue)
        {
            textEdit.PreviewKeyDown += OnPreviewKeyDown;
            textEdit.GotFocus += OnGotFocus;
        }
        else
        {
            textEdit.PreviewKeyDown -= OnPreviewKeyDown;
            textEdit.GotFocus -= OnGotFocus;
        }
    }

    private static void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextEdit textEdit)
        {
            // Select all text when focused for easy replacement
            textEdit.SelectAll();
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var textEdit = (TextEdit)sender;
        var key = e.Key == Key.System
            ? e.SystemKey
            : e.Key;

        // Ignore modifier-only key presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        // Allow clearing with Delete, Backspace, or Escape
        if (key is Key.Delete or Key.Back or Key.Escape)
        {
            textEdit.Text = string.Empty;
            return;
        }

        // Build the hotkey string
        var modifiers = ModifierKeys.None;
        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            modifiers |= ModifierKeys.Control;

        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))
            modifiers |= ModifierKeys.Alt;

        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            modifiers |= ModifierKeys.Shift;

        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows))
            modifiers |= ModifierKeys.Windows;

        // Get the virtual key code
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);

        // Build the hotkey string
        var hotkeyString = HotkeyParser.ToString(modifiers, virtualKey);

        // Set the hotkey string
        textEdit.Text = hotkeyString;

        // Note: Validation is handled by the binding, not here
        // DevExpress HasValidationError is read-only and cannot be set directly
    }
}
