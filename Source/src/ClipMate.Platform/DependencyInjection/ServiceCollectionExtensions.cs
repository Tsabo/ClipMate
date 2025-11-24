using Microsoft.Extensions.DependencyInjection;
using ClipMate.Core.Services;
using ClipMate.Platform.Interop;
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
        // Register Win32 interop wrappers for testability
        services.AddSingleton<IWin32ClipboardInterop, Win32ClipboardInterop>();
        services.AddSingleton<IWin32HotkeyInterop, Win32HotkeyInterop>();
        services.AddSingleton<IWin32InputInterop, Win32InputInterop>();

        // Register ClipboardService as singleton since it manages Win32 resources
        services.AddSingleton<IClipboardService, ClipboardService>();

        // Register HotkeyManager as singleton for global hotkey management
        services.AddSingleton<HotkeyManager>();

        // Register HotkeyService as singleton for global hotkey management
        services.AddSingleton<IHotkeyService, HotkeyService>();

        // Register PasteService for PowerPaste functionality
        services.AddSingleton<IPasteService, PasteService>();

        // Note: SystemTrayService removed - now using WPF-UI.Tray NotifyIcon component

        // Future platform services will be registered here
        // services.AddSingleton<ISoundService, SoundService>();

        return services;
    }
}
