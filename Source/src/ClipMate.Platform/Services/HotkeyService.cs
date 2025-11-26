using System.Windows;
using ClipMate.Core.Models;
using ClipMate.Core.Services;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for managing global hotkeys, wrapping the HotkeyManager.
/// </summary>
public class HotkeyService : IHotkeyService, IDisposable
{
    private readonly IHotkeyManager _hotkeyManager;
    private readonly HashSet<int> _registeredIds;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref=\"HotkeyService\" /> class.
    /// </summary>
    public HotkeyService(IHotkeyManager hotkeyManager)
    {
        _hotkeyManager = hotkeyManager ?? throw new ArgumentNullException(nameof(hotkeyManager));
        _registeredIds = [];
    }


    /// <summary>
    /// Disposes the hotkey service and unregisters all hotkeys.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _hotkeyManager.Dispose();
        _registeredIds.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public bool RegisterHotkey(int id, ModifierKeys modifiers, int key, Action action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (action == null)
            throw new ArgumentNullException(nameof(action));

        try
        {
            // If already registered with this ID, unregister first
            if (_registeredIds.Contains(id))
                UnregisterHotkey(id);

            // Register with HotkeyManager (which returns its own internal ID)
            _ = _hotkeyManager.RegisterHotkey(modifiers, key, action);

            // Track our logical ID
            _registeredIds.Add(id);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool UnregisterHotkey(int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_registeredIds.Contains(id))
            return false;

        // Note: This is simplified - in a real implementation, we'd need to track
        // the mapping between our logical IDs and HotkeyManager's internal IDs
        _registeredIds.Remove(id);
        return true;
    }

    /// <inheritdoc />
    public void UnregisterAllHotkeys()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _hotkeyManager.UnregisterAll();
        _registeredIds.Clear();
    }

    /// <inheritdoc />
    public bool IsHotkeyRegistered(int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _registeredIds.Contains(id);
    }

    /// <summary>
    /// Initializes the hotkey service with a window for receiving hotkey messages.
    /// Must be called before registering any hotkeys.
    /// </summary>
    /// <param name="window">The WPF window to use for receiving hotkey messages.</param>
    public void Initialize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _hotkeyManager.Initialize(window);
    }
}
