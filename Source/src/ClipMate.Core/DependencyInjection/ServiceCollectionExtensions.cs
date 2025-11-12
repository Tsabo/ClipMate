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

        // Service interfaces will be registered here as they are implemented
        // Example:
        // services.AddSingleton<IClipboardService, ClipboardService>();
        // services.AddSingleton<IClipService, ClipService>();
        // services.AddSingleton<ICollectionService, CollectionService>();
        // services.AddSingleton<IFolderService, FolderService>();
        // services.AddSingleton<ISearchService, SearchService>();
        // services.AddSingleton<ITemplateService, TemplateService>();
        // services.AddSingleton<IHotkeyService, HotkeyService>();
        // services.AddSingleton<ISoundService, SoundService>();
        // services.AddSingleton<ISettingsService, SettingsService>();
        // services.AddSingleton<IApplicationFilterService, ApplicationFilterService>();

        return services;
    }
}
