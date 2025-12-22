using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        // Register MVVM Community Toolkit messenger as singleton
        // Using WeakReferenceMessenger for automatic memory management
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Register text transform service as singleton
        services.AddSingleton<ITextTransformService, TextTransformService>();

        // Register application profile services as singletons
        // These use TOML file storage, not EF Core, so they're stateless and thread-safe
        // Profiles are stored in LocalApplicationData (not roaming) as they're machine-specific
        var profilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate",
            "application-profiles.toml");

        services.AddSingleton<IApplicationProfileStore>(p =>
            new ApplicationProfileStore(profilesPath, p.GetRequiredService<ILogger<ApplicationProfileStore>>()));

        services.AddSingleton<IApplicationProfileService, ApplicationProfileService>();

        // Register SearchResultsCache as singleton (maintains per-database search result cache)
        services.AddSingleton<SearchResultsCache>();

        // Note: Service implementations are registered in ClipMate.Data
        // via AddClipMateData() extension method

        return services;
    }
}
