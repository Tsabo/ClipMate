using ClipMate.Core.Services;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using Microsoft.Extensions.DependencyInjection;

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

        // Register clipboard format enumerator
        services.AddSingleton<IClipboardFormatEnumerator, ClipboardFormatEnumerator>();

        // Register ClipboardService as singleton since it manages Win32 resources
        services.AddSingleton<IClipboardService, ClipboardService>();

        // Register HotkeyManager as singleton for global hotkey management
        services.AddSingleton<IHotkeyManager, HotkeyManager>();

        // Register HotkeyService as singleton for global hotkey management
        services.AddSingleton<IHotkeyService, HotkeyService>();

        // Register PasteService for PowerPaste functionality
        services.AddSingleton<IPasteService, PasteService>();

        // Register QuickPasteService for QuickPaste functionality
        services.AddSingleton<IQuickPasteService, QuickPasteService>();

        // Register MacroExecutionService as singleton for keystroke sending
        services.AddSingleton<IMacroExecutionService, MacroExecutionService>();

        // Register SoundService for audio feedback
        services.AddSingleton<ISoundService, SoundService>();

        // Register StartupManager for Windows startup configuration
        services.AddSingleton<IStartupManager, StartupManager>();

        // Register FileLoggingService for log management
        services.AddSingleton<IFileLoggingService, FileLoggingService>();

        // Register Win32IdleDetector for system idle detection
        services.AddSingleton<IWin32IdleDetector, Win32IdleDetector>();

        // Register ExportImportService as transient - only used occasionally for export/import operations
        services.AddTransient<IExportImportService, ExportImportService>();

        // Register diagnostic services
        services.AddSingleton<IClipboardDiagnosticsService, ClipboardDiagnosticsService>();
        services.AddSingleton<IPasteTraceService, PasteTraceService>();

        // Note: SystemTrayService removed - now using WPF-UI.Tray NotifyIcon component

        return services;
    }
}
