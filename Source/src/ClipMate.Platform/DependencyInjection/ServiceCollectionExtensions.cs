using Microsoft.Extensions.DependencyInjection;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;

namespace ClipMate.Platform.DependencyInjection;

/// <summary>
/// Extension methods for registering Platform layer services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ClipMate Platform services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddClipMatePlatform(this IServiceCollection services)
    {
        // Register ClipboardService as singleton since it manages Win32 resources
        services.AddSingleton<IClipboardService, ClipboardService>();

        // Register HotkeyService as singleton for global hotkey management
        services.AddSingleton<IHotkeyService, HotkeyService>();

        // Register PasteService for PowerPaste functionality
        services.AddSingleton<IPasteService, PasteService>();

        // Register SystemTrayService as singleton (manages system tray icon lifecycle)
        services.AddSingleton<SystemTrayService>();

        // Future platform services will be registered here
        // services.AddSingleton<ISoundService, SoundService>();

        return services;
    }
}
