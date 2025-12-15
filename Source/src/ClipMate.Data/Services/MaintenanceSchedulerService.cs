using ClipMate.Core.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Background service that schedules periodic database maintenance tasks.
/// Runs retention enforcement and cleanup only when the system is idle.
/// </summary>
public class MaintenanceSchedulerService : IHostedService, IDisposable
{
    private readonly IWin32IdleDetector _idleDetector;
    private readonly TimeSpan _idleThreshold;
    private readonly ILogger<MaintenanceSchedulerService> _logger;
    private readonly TimeSpan _maintenanceInterval;
    private readonly IRetentionEnforcementService _retentionService;
    private Timer? _timer;

    public MaintenanceSchedulerService(IRetentionEnforcementService retentionService,
        IWin32IdleDetector idleDetector,
        ILogger<MaintenanceSchedulerService> logger)
    {
        _retentionService = retentionService ?? throw new ArgumentNullException(nameof(retentionService));
        _idleDetector = idleDetector ?? throw new ArgumentNullException(nameof(idleDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default: Run maintenance every hour
        _maintenanceInterval = TimeSpan.FromHours(1);

        // Default: Wait for 5 minutes of idle time before running maintenance
        _idleThreshold = TimeSpan.FromMinutes(5);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Maintenance scheduler started. Interval: {Interval}, Idle threshold: {IdleThreshold}",
            _maintenanceInterval,
            _idleThreshold);

        _timer = new Timer(
            RunMaintenance,
            null,
            _maintenanceInterval,
            _maintenanceInterval);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Maintenance scheduler stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void RunMaintenance(object? state)
    {
        try
        {
            // Check if system is idle
            if (!_idleDetector.IsIdle(_idleThreshold))
            {
                _logger.LogDebug(
                    "Skipping maintenance - system not idle (threshold: {IdleThreshold})",
                    _idleThreshold);

                return;
            }

            _logger.LogInformation("Starting scheduled maintenance");

            // TODO: Get database key from configuration or context
            const string databaseKey = "default";

            // Run retention enforcement across all collections
            var clipsProcessed = await _retentionService.EnforceAllCollectionsAsync(databaseKey);

            _logger.LogInformation(
                "Maintenance completed. Clips moved/deleted: {ClipsProcessed}",
                clipsProcessed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled maintenance");
        }
    }
}
