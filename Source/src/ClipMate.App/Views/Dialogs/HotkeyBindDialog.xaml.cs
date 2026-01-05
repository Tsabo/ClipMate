using System.Windows.Input;
using System.Windows.Media;
using ClipMate.App.Helpers;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ModifierKeys = ClipMate.Core.Models.ModifierKeys;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for capturing hotkey input with toggle modifiers and special key support.
/// </summary>
public partial class HotkeyBindDialog
{
    private readonly List<SpecialKeyItem> _specialKeys =
    [
        new("Media Next Track", Key.MediaNextTrack),
        new("Media Previous Track", Key.MediaPreviousTrack),
        new("Media Play/Pause", Key.MediaPlayPause),
        new("Media Stop", Key.MediaStop),
        new(IsSeparator: true),
        new("Volume Up", Key.VolumeUp),
        new("Volume Down", Key.VolumeDown),
        new("Volume Mute", Key.VolumeMute),
        new(IsSeparator: true),
        new("Browser Back", Key.BrowserBack),
        new("Browser Forward", Key.BrowserForward),
        new("Browser Favorites", Key.BrowserFavorites),
        new("Browser Home", Key.BrowserHome),
        new("Browser Refresh", Key.BrowserRefresh),
        new("Browser Search", Key.BrowserSearch),
        new("Browser Stop", Key.BrowserStop),
        new(IsSeparator: true),
        new("Launch Mail", Key.LaunchMail),
        new("Launch Media Select", Key.SelectMedia),
        new("Launch App 1", Key.LaunchApplication1),
        new("Launch App 2", Key.LaunchApplication2),
    ];

    private Key? _currentKey;
    private bool _isAltPressed;
    private bool _isCtrlPressed;

    private bool _isShiftPressed;
    private bool _isWinPressed;

    public HotkeyBindDialog()
    {
        InitializeComponent();
        PopulateSpecialKeysMenu();
        KeyTextBox.Text = string.Empty;
        UpdateModifierButtonStates();
    }

    public string? CapturedHotkey { get; private set; }

    private void PopulateSpecialKeysMenu()
    {
        foreach (var item in _specialKeys)
        {
            if (item.IsSeparator)
            {
                SpecialKeyPopupMenu.Items.Add(new BarItemSeparator());
                continue;
            }

            var menuItem = new BarButtonItem
            {
                Content = item.DisplayName,
                Tag = item.Key,
            };
            menuItem.ItemClick += SpecialKeyMenuItem_ItemClick;
            SpecialKeyPopupMenu.Items.Add(menuItem);
        }
    }

    public void SetInitialHotkey(string? hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return;

        // Parse the hotkey string
        if (!HotkeyParser.TryParse(hotkeyString, out var modifiers, out var virtualKey, out var _))
            return;

        _isShiftPressed = modifiers.HasFlag(ModifierKeys.Shift);
        _isCtrlPressed = modifiers.HasFlag(ModifierKeys.Control);
        _isWinPressed = modifiers.HasFlag(ModifierKeys.Windows);
        _isAltPressed = modifiers.HasFlag(ModifierKeys.Alt);

        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        _currentKey = key;
        KeyTextBox.Text = GetKeyDisplayName(key);

        UpdateModifierButtonStates();
        UpdateCapturedHotkey();
    }

    private void ModifierButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not SimpleButton { Tag: string modifier })
            return;

        // Toggle the modifier state
        switch (modifier)
        {
            case "Shift":
                _isShiftPressed = !_isShiftPressed;
                break;
            case "Ctrl":
                _isCtrlPressed = !_isCtrlPressed;
                break;
            case "Win":
                _isWinPressed = !_isWinPressed;
                break;
            case "Alt":
                _isAltPressed = !_isAltPressed;
                break;
        }

        UpdateModifierButtonStates();
        UpdateCapturedHotkey();
    }

    private void KeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System
            ? e.SystemKey
            : e.Key;

        // Ignore modifier-only presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        // Ignore Tab (to allow dialog navigation)
        if (key == Key.Tab)
        {
            e.Handled = false;
            return;
        }

        // Clear on Delete/Backspace
        if (key is Key.Delete or Key.Back)
        {
            _currentKey = null;
            KeyTextBox.Text = string.Empty;
            UpdateCapturedHotkey();
            return;
        }

        _currentKey = key;
        KeyTextBox.Text = GetKeyDisplayName(key);
        UpdateCapturedHotkey();
    }

    private void SpecialKeyMenuItem_ItemClick(object? sender, ItemClickEventArgs e)
    {
        if (e.Item.Tag is not Key key)
            return;

        _currentKey = key;
        KeyTextBox.Text = GetKeyDisplayName(key);
        UpdateCapturedHotkey();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        _isShiftPressed = false;
        _isCtrlPressed = false;
        _isWinPressed = false;
        _isAltPressed = false;
        _currentKey = null;
        KeyTextBox.Text = string.Empty;
        UpdateModifierButtonStates();
        UpdateCapturedHotkey();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void UpdateModifierButtonStates()
    {
        // Update button appearance based on toggle state
        UpdateButtonState(ShiftButton, _isShiftPressed);
        UpdateButtonState(CtrlButton, _isCtrlPressed);
        UpdateButtonState(WinButton, _isWinPressed);
        UpdateButtonState(AltButton, _isAltPressed);
    }

    private void UpdateButtonState(SimpleButton button, bool isPressed)
    {
        if (isPressed)
        {
            button.Background = new SolidColorBrush(WpfColor.FromRgb(173, 216, 230)); // Light blue
            button.Foreground = WpfBrushes.Black;
        }
        else
        {
            button.ClearValue(BackgroundProperty);
            button.ClearValue(ForegroundProperty);
        }
    }

    private void UpdateCapturedHotkey()
    {
        if (_currentKey == null)
        {
            CapturedHotkey = null;
            return;
        }

        var modifiers = ModifierKeys.None;
        if (_isCtrlPressed)
            modifiers |= ModifierKeys.Control;

        if (_isAltPressed)
            modifiers |= ModifierKeys.Alt;

        if (_isShiftPressed)
            modifiers |= ModifierKeys.Shift;

        if (_isWinPressed)
            modifiers |= ModifierKeys.Windows;

        var virtualKey = KeyInterop.VirtualKeyFromKey(_currentKey.Value);
        CapturedHotkey = HotkeyParser.ToString(modifiers, virtualKey);
    }

    private static string GetKeyDisplayName(Key key)
    {
        // Return friendly names for special keys
        return key switch
        {
            Key.MediaNextTrack => "Media Next Track",
            Key.MediaPreviousTrack => "Media Previous Track",
            Key.MediaPlayPause => "Media Play/Pause",
            Key.MediaStop => "Media Stop",
            Key.VolumeUp => "Volume Up",
            Key.VolumeDown => "Volume Down",
            Key.VolumeMute => "Volume Mute",
            Key.BrowserBack => "Browser Back",
            Key.BrowserForward => "Browser Forward",
            Key.BrowserFavorites => "Browser Favorites",
            Key.BrowserHome => "Browser Home",
            Key.BrowserRefresh => "Browser Refresh",
            Key.BrowserSearch => "Browser Search",
            Key.BrowserStop => "Browser Stop",
            Key.LaunchMail => "Launch Mail",
            Key.SelectMedia => "Launch Media Select",
            Key.LaunchApplication1 => "Launch App 1",
            Key.LaunchApplication2 => "Launch App 2",
            var _ => key.ToString(),
        };
    }

    private record SpecialKeyItem(string DisplayName = "", Key Key = Key.None, bool IsSeparator = false);
}
