using System.Threading;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services;

/// <summary>
/// Background service that periodically checks for application updates from GitHub releases.
/// </summary>
public class UpdateCheckerService : IHostedService, IRecipient<PreferencesChangedEvent>, IDisposable
{
    private readonly IConfigurationService _configurationService;
    private readonly IMessenger _messenger;
    private readonly ILogger<UpdateCheckerService> _logger;
    private System.Threading.Timer? _timer;
    private bool _disposed;

    public UpdateCheckerService(
        IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<UpdateCheckerService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the update checker service and registers for preference change events.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UpdateCheckerService starting");
        
        // Register to receive preference change events
        _messenger.Register(this);

        // Start the timer for periodic checks
        RestartTimer();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the update checker service and unregisters from events.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UpdateCheckerService stopping");
        
        // Unregister from messenger
        _messenger.Unregister<PreferencesChangedEvent>(this);

        // Stop the timer
        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles preference change events by reloading configuration and restarting the timer.
    /// </summary>
    public void Receive(PreferencesChangedEvent message)
    {
        _logger.LogDebug("Preferences changed, restarting update checker timer");
        RestartTimer();
    }

    /// <summary>
    /// Restarts the timer based on current preferences.
    /// </summary>
    private void RestartTimer()
    {
        var config = _configurationService.Configuration;
        var preferences = config.Preferences;

        // Dispose existing timer
        _timer?.Dispose();

        if (!preferences.CheckUpdatesAutomatically)
        {
            _logger.LogInformation("Automatic update checking is disabled");
            return;
        }

        // Calculate time until next check
        var now = DateTime.UtcNow;
        var lastCheck = preferences.LastUpdateCheckDate ?? DateTime.MinValue;
        var intervalDays = preferences.UpdateCheckIntervalDays;
        var nextCheck = lastCheck.AddDays(intervalDays);

        TimeSpan dueTime;
        if (nextCheck <= now)
        {
            // If we're past due, check immediately
            dueTime = TimeSpan.Zero;
            _logger.LogInformation("Update check is overdue, checking immediately");
        }
        else
        {
            // Otherwise, wait until the next check is due
            dueTime = nextCheck - now;
            _logger.LogInformation("Next update check scheduled in {Days} days", dueTime.TotalDays);
        }

        // Create timer with the calculated due time and the interval from preferences
        var period = TimeSpan.FromDays(intervalDays);
        _timer = new System.Threading.Timer(CheckForUpdates, null, dueTime, period);
    }

    /// <summary>
    /// Timer callback that performs the update check.
    /// </summary>
    private async void CheckForUpdates(object? state)
    {
        try
        {
            _logger.LogInformation("Checking for updates");

            var config = _configurationService.Configuration;
            
            // Update last check date
            config.Preferences.LastUpdateCheckDate = DateTime.UtcNow;
            await _configurationService.SaveAsync();

            // TODO: Implement actual GitHub API call to check for updates
            // URL: https://api.github.com/repos/Tsabo/ClipMate/releases/latest
            // Compare version numbers and notify user if update is available
            // Use DevExpress ToastNotificationManager to show notification to user

            _logger.LogDebug("Update check completed (stub implementation)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
        }
    }

    /// <summary>
    /// Disposes the service and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _timer?.Dispose();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}
