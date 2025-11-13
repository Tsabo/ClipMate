using Microsoft.Extensions.DependencyInjection;
using ClipMate.Core.Events;
using ClipMate.Core.Services;

namespace ClipMate.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering Core services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ClipMate Core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddClipMateCore(this IServiceCollection services)
    {
        // Register event aggregator as singleton
        services.AddSingleton<IEventAggregator, EventAggregator>();

        // Register text transform service as singleton
        services.AddSingleton<TextTransformService>();

        // Note: Service implementations are registered in ClipMate.Data
        // via AddClipMateData() extension method

        return services;
    }
}
