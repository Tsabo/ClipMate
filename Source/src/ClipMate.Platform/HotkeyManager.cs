using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipMate.Platform;

/// <summary>
/// Manages global hotkey registration and handling.
/// </summary>
public class HotkeyManager : IDisposable
{
    private HwndSource? _hwndSource;
    private readonly Dictionary<int, HotkeyRegistration> _registeredHotkeys;
    private int _nextHotkeyId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HotkeyManager"/> class.
    /// </summary>
    public HotkeyManager()
    {
        _registeredHotkeys = new Dictionary<int, HotkeyRegistration>();
        _nextHotkeyId = 1;
    }

    /// <summary>
    /// Initializes the hotkey manager with a window.
    /// </summary>
    /// <param name="window">The WPF window to use for receiving hotkey messages.</param>
    public void Initialize(Window window)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hwndSource != null)
        {
            throw new InvalidOperationException("HotkeyManager is already initialized.");
        }

        if (window == null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        // Get the window handle
        var windowInteropHelper = new WindowInteropHelper(window);
        var hwnd = windowInteropHelper.Handle;

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not available. Ensure the window is loaded.");
        }

        // Create HwndSource to intercept Windows messages
        _hwndSource = HwndSource.FromHwnd(hwnd);
        if (_hwndSource == null)
        {
            throw new InvalidOperationException("Failed to create HwndSource from window handle.");
        }

        // Add hook to process WM_HOTKEY messages
        _hwndSource.AddHook(WndProc);
    }

    /// <summary>
    /// Registers a global hotkey.
    /// </summary>
    /// <param name="modifiers">The modifier keys (Alt, Ctrl, Shift, Win).</param>
    /// <param name="key">The virtual key code.</param>
    /// <param name="callback">The action to execute when the hotkey is pressed.</param>
    /// <returns>The hotkey ID that can be used to unregister the hotkey.</returns>
    public int RegisterHotkey(ModifierKeys modifiers, int key, Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hwndSource == null)
        {
            throw new InvalidOperationException("HotkeyManager is not initialized. Call Initialize() first.");
        }

        if (callback == null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        var hwnd = _hwndSource.Handle;
        var hotkeyId = _nextHotkeyId++;
        var modifierFlags = ConvertModifiers(modifiers);

        if (!PInvoke.RegisterHotKey(new HWND(hwnd), hotkeyId, (HOT_KEY_MODIFIERS)modifierFlags, (uint)key))
        {
            throw new InvalidOperationException(
                $"Failed to register hotkey (Modifiers: {modifiers}, Key: {key}). " +
                "The hotkey may already be in use by another application.");
        }

        _registeredHotkeys[hotkeyId] = new HotkeyRegistration
        {
            Id = hotkeyId,
            Modifiers = modifiers,
            Key = key,
            Callback = callback
        };

        return hotkeyId;
    }

    /// <summary>
    /// Unregisters a previously registered hotkey.
    /// </summary>
    /// <param name="hotkeyId">The hotkey ID returned from RegisterHotkey.</param>
    /// <returns>True if the hotkey was successfully unregistered; otherwise, false.</returns>
    public bool UnregisterHotkey(int hotkeyId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hwndSource == null || !_registeredHotkeys.ContainsKey(hotkeyId))
        {
            return false;
        }

        var hwnd = _hwndSource.Handle;
        var result = PInvoke.UnregisterHotKey(new HWND(hwnd), hotkeyId);

        if (result)
        {
            _registeredHotkeys.Remove(hotkeyId);
        }

        return result;
    }

    /// <summary>
    /// Unregisters all hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        if (_hwndSource == null)
        {
            return;
        }

        var hwnd = _hwndSource.Handle;
        var hotkeyIds = _registeredHotkeys.Keys.ToList();

        foreach (var hotkeyId in hotkeyIds)
        {
            PInvoke.UnregisterHotKey(new HWND(hwnd), hotkeyId);
        }

        _registeredHotkeys.Clear();
    }

    /// <summary>
    /// Window procedure to handle WM_HOTKEY messages.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        
        if (msg == WM_HOTKEY)
        {
            var hotkeyId = wParam.ToInt32();

            if (_registeredHotkeys.TryGetValue(hotkeyId, out var registration))
            {
                // Execute callback on the UI thread
                Application.Current?.Dispatcher.BeginInvoke(registration.Callback);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Converts ModifierKeys enum to Win32 modifier flags.
    /// </summary>
    private static uint ConvertModifiers(ModifierKeys modifiers)
    {
        const uint MOD_ALT = 0x0001;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint MOD_WIN = 0x0008;
        
        uint flags = 0;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            flags |= MOD_ALT;
        }
        
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            flags |= MOD_CONTROL;
        }
        
        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            flags |= MOD_SHIFT;
        }
        
        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            flags |= MOD_WIN;
        }

        return flags;
    }

    /// <summary>
    /// Disposes the hotkey manager and unregisters all hotkeys.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnregisterAll();

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents a registered hotkey.
    /// </summary>
    private class HotkeyRegistration
    {
        public int Id { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public int Key { get; set; }
        public Action Callback { get; set; } = null!;
    }
}

/// <summary>
/// Modifier keys for hotkeys.
/// </summary>
[Flags]
public enum ModifierKeys
{
    /// <summary>
    /// No modifier.
    /// </summary>
    None = 0,

    /// <summary>
    /// Alt key.
    /// </summary>
    Alt = 1,

    /// <summary>
    /// Control key.
    /// </summary>
    Control = 2,

    /// <summary>
    /// Shift key.
    /// </summary>
    Shift = 4,

    /// <summary>
    /// Windows key.
    /// </summary>
    Windows = 8
}
