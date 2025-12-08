using System.Windows;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for managing global hotkeys, wrapping the HotkeyManager.
/// </summary>
public class HotkeyService : IHotkeyService, IDisposable
{
    private readonly IHotkeyManager _hotkeyManager;

    // Map logical hotkey IDs -> internal HotkeyManager IDs so we can unregister correctly
    private readonly Dictionary<int, int> _idToInternalId;
    private readonly ILogger<HotkeyService> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HotkeyService" /> class.
    /// </summary>
    public HotkeyService(ILogger<HotkeyService> logger, IHotkeyManager hotkeyManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hotkeyManager = hotkeyManager ?? throw new ArgumentNullException(nameof(hotkeyManager));
        _idToInternalId = new Dictionary<int, int>();
    }

    /// <summary>
    /// Convenience constructor for scenarios (like tests) where a logger isn't provided.
    /// </summary>
    public HotkeyService(IHotkeyManager hotkeyManager)
        : this(NullLogger<HotkeyService>.Instance, hotkeyManager)
    {
    }

    /// <summary>
    /// Disposes the hotkey service and unregisters all hotkeys.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        UnregisterAllHotkeys();
        _hotkeyManager.Dispose();
        _idToInternalId.Clear();
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
            // If already registered with this ID, unregister first (removes prior OS registration)
            if (_idToInternalId.ContainsKey(id))
                UnregisterHotkey(id);

            // Register with HotkeyManager (returns internal OS registration ID)
            var internalId = _hotkeyManager.RegisterHotkey(modifiers, key, action);

            // Track mapping so we can properly unregister later
            _idToInternalId[id] = internalId;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register hotkey (ID: {HotkeyId}, Modifiers: {Modifiers}, Key: {Key}, Action: {Action}", id, modifiers, key, action);
            return false;
        }
    }

    /// <inheritdoc />
    public bool UnregisterHotkey(int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_idToInternalId.TryGetValue(id, out var internalId))
            return false;

        var success = _hotkeyManager.UnregisterHotkey(internalId);
        _idToInternalId.Remove(id);

        return success;
    }

    /// <inheritdoc />
    public void UnregisterAllHotkeys()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _hotkeyManager.UnregisterAll();
        _idToInternalId.Clear();
    }

    /// <inheritdoc />
    public bool IsHotkeyRegistered(int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _idToInternalId.ContainsKey(id);
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
