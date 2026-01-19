using System.Runtime.InteropServices;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Monitors Windows session changes (lock/unlock) using WTS (Windows Terminal Services) notifications.
/// </summary>
public sealed class SessionMonitorService : ISessionMonitorService, IDisposable
{
    private const int _wmWtsSessionChange = 0x02B1;
    private const int _wtsSessionLock = 0x7;
    private const int _wtsSessionUnlock = 0x8;
    private const int _notifyForThisSession = 0;

    private readonly IConfigurationService _configurationService;
    private readonly IMessenger _messenger;
    private readonly ILogger<SessionMonitorService>? _logger;
    private bool _isRegistered;
    private nint _windowHandle;

    public SessionMonitorService(IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<SessionMonitorService>? logger = null)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger;
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Starts monitoring session changes for the specified window.
    /// </summary>
    public void Start(nint windowHandle)
    {
        if (_isRegistered)
        {
            _logger?.LogWarning("SessionMonitorService already started");
            return;
        }

        _windowHandle = windowHandle;

        // Register for session notifications
        if (!WTSRegisterSessionNotification(_windowHandle, _notifyForThisSession))
        {
            _logger?.LogError("Failed to register for WTS session notifications");
            return;
        }

        _isRegistered = true;
        _logger?.LogInformation("Session monitoring started");
    }

    /// <summary>
    /// Stops monitoring session changes.
    /// </summary>
    public void Stop()
    {
        if (!_isRegistered)
            return;

        if (_windowHandle != IntPtr.Zero)
        {
            try
            {
                // Unregister session notifications
                WTSUnregisterSessionNotification(_windowHandle);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to unregister WTS session notifications (handle may be invalid)");
            }
            finally
            {
                _windowHandle = IntPtr.Zero;
            }
        }

        _isRegistered = false;
        _logger?.LogInformation("Session monitoring stopped");
    }

    /// <summary>
    /// Processes a window message to check for session change notifications.
    /// </summary>
    public bool ProcessMessage(int msg, nint wParam, nint lParam)
    {
        if (msg != _wmWtsSessionChange)
            return false;

        var sessionChangeReason = (int)wParam;

        switch (sessionChangeReason)
        {
            case _wtsSessionLock:
                OnSessionLocked();
                return true;

            case _wtsSessionUnlock:
                _logger?.LogDebug("Session unlocked");
                return true;

            default:
                return false;
        }
    }

    private void OnSessionLocked()
    {
        _logger?.LogInformation("Session locked");

        // Check if lock-on-screen-lock feature is enabled
        if (!_configurationService.Configuration.Encryption.LockOnScreenLock)
        {
            _logger?.LogDebug("LockOnScreenLock disabled, skipping automatic lock");
            return;
        }

        // Send lock request message to lock all decrypted clips and forget encryption key
        // LockAll=true ensures the key is forgotten
        _messenger.Send(new LockClipsRequestedEvent([], true));
        _logger?.LogDebug("Sent LockClipsRequestedEvent (lock all) due to session lock");
    }

    // P/Invoke declarations for WTS APIs
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSRegisterSessionNotification(nint hWnd, int dwFlags);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSUnregisterSessionNotification(nint hWnd);
}
