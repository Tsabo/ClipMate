using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Executes all registered startup initialization steps in order.
/// Provides logging and error handling for the initialization process.
/// </summary>
public class StartupInitializationPipeline
{
    private readonly ILogger<StartupInitializationPipeline> _logger;
    private readonly IEnumerable<IStartupInitializationStep> _steps;

    public StartupInitializationPipeline(IEnumerable<IStartupInitializationStep> steps,
        ILogger<StartupInitializationPipeline> logger)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs all initialization steps in order.
    /// Throws an exception if any step fails.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var orderedSteps = _steps.OrderBy(p => p.Order).ToList();

        _logger.LogInformation("Starting initialization pipeline with {StepCount} steps", orderedSteps.Count);

        foreach (var step in orderedSteps)
        {
            _logger.LogInformation("Running initialization step: {StepName} (Order: {Order})", step.Name, step.Order);

            try
            {
                await step.InitializeAsync(cancellationToken);
                _logger.LogInformation("Completed initialization step: {StepName}", step.Name);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Initialization step '{StepName}' failed", step.Name);
                throw new InvalidOperationException($"Startup initialization failed at step '{step.Name}'", ex);
            }
        }

        _logger.LogInformation("Initialization pipeline completed successfully");
    }
}
