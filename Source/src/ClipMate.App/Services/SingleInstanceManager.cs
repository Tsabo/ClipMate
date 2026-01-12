using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Services;

/// <summary>
/// Manages single instance enforcement using a named mutex.
/// Ensures only one instance of ClipMate can run at a time.
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private const string MutexName = "Global\\ClipMate_SingleInstance_Mutex";

    private readonly ILogger<SingleInstanceManager> _logger;
    private bool _isOwner;
    private Mutex? _mutex;

    public SingleInstanceManager(ILogger<SingleInstanceManager> logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        if (_mutex != null && _isOwner)
        {
            try
            {
                _mutex.ReleaseMutex();
                _logger.LogDebug("Single instance mutex released");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release single instance mutex");
            }
        }

        _mutex?.Dispose();
    }

    /// <summary>
    /// Attempts to acquire the single instance mutex.
    /// </summary>
    /// <returns>True if this is the first instance, false if another instance is already running.</returns>
    public bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out _isOwner);

            if (!_isOwner)
            {
                _logger.LogInformation("Another instance of ClipMate is already running");
                return false;
            }

            _logger.LogDebug("Single instance mutex acquired successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create single instance mutex");
            return true; // Allow app to start if mutex creation fails
        }
    }

    /// <summary>
    /// Shows a message to the user indicating another instance is running.
    /// </summary>
    public static void ShowAlreadyRunningMessage()
    {
        MessageBox.Show(
            "ClipMate is already running. Please check the system tray.",
            "ClipMate",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
