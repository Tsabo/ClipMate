using ClipMate.App.Services;
using ClipMate.App.Services.Initialization;
using ClipMate.App.ViewModels;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ClipMate.App.DependencyInjection;

/// <summary>
/// Extension methods for registering ClipMate.App layer services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all ClipMate.App layer services including ViewModels, Windows, Coordinators, and Initialization steps.
    /// </summary>
    public static IServiceCollection AddClipMateApp(this IServiceCollection services)
    {
        // Application Host
        services.AddHostedService<ApplicationHostService>();

        // Register Dialog service (App-layer implementation for Platform layer)
        services.AddSingleton<IDialogService, DialogService>();

        // Register active window tracking service
        services.AddSingleton<IActiveWindowService, ActiveWindowService>();

        // Register Update Checker as hosted service
        services.AddHostedService<UpdateCheckerService>();

        // Register MVVM Toolkit Messenger as singleton
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Register the shared EventLogSink instance (already receiving logs via Serilog sink)
        services.AddSingleton<IEventLogSink>(p => p.GetRequiredService<EventLogSink>());

        // Register ClipBar (quick paste picker) as hosted service
        services.AddSingleton<ClassicWindowCoordinator>();
        services.AddHostedService(p => p.GetRequiredService<ClassicWindowCoordinator>());

        // Register ExplorerWindow as singleton (always exists, just hidden/shown)
        services.AddSingleton<ExplorerWindow>();
        services.AddSingleton<IWindow, ExplorerWindow>(p => p.GetRequiredService<ExplorerWindow>());

        // Register TrayIconWindow (system tray icon)
        services.AddTransient<TrayIconWindow>();

        // Register ClipBar (quick paste picker) components
        services.AddTransient<ClassicViewModel>();
        services.AddTransient<ClassicWindow>();

        // Register Collection Tree Builder
        services.AddTransient<ICollectionTreeBuilder, CollectionTreeBuilder>();

        // Register ViewModels
        services.AddSingleton<MainMenuViewModel>(); // Shared menu ViewModel
        services.AddSingleton<ExplorerWindowViewModel>();
        services.AddSingleton<CollectionTreeViewModel>();
        services.AddSingleton<ClipListViewModel>();
        services.AddSingleton<PreviewPaneViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<QuickPasteToolbarViewModel>();
        services.AddTransient<ClipViewerToolbarViewModel>();
        services.AddTransient<ClipPropertiesViewModel>();
        services.AddTransient<RenameClipDialogViewModel>();
        services.AddTransient<ClipViewerViewModel>();

        // Register Clip Viewer factory and manager
        services.AddSingleton<Func<ClipViewerViewModel>>(p => p.GetRequiredService<ClipViewerViewModel>);
        services.AddSingleton<IClipViewerWindowManager, ClipViewerWindowManager>();

        // Register Text Tools components
        services.AddTransient<TextToolsViewModel>();
        services.AddTransient<TextToolsDialog>();

        // Register Diagnostic ViewModels
        services.AddTransient<ClipboardDiagnosticsViewModel>();
        services.AddTransient<EventLogViewModel>();
        services.AddTransient<PasteTraceViewModel>();
        services.AddTransient<SqlMaintenanceViewModel>();
        services.AddTransient<AboutDialogViewModel>();

        // Register Text Cleanup dialog (no ViewModel - uses code-behind)
        services.AddTransient<TextCleanupDialog>();

        // Register Options dialog components
        services.AddTransient<GeneralOptionsViewModel>();
        services.AddTransient<PowerPasteOptionsViewModel>();
        services.AddTransient<QuickPasteOptionsViewModel>();
        services.AddTransient<EditorOptionsViewModel>();
        services.AddTransient<CapturingOptionsViewModel>();
        services.AddTransient<ApplicationProfilesOptionsViewModel>();
        services.AddTransient<SoundsOptionsViewModel>();
        services.AddTransient<HotkeysOptionsViewModel>();
        services.AddTransient<DatabaseOptionsViewModel>();
        services.AddTransient<AdvancedOptionsViewModel>();
        services.AddTransient<OptionsViewModel>();
        services.AddTransient<OptionsDialog>();

        // Register coordinators
        services.AddSingleton<HotkeyCoordinator>();
        services.AddSingleton<DatabaseMaintenanceCoordinator>();
        services.AddSingleton<ClipOperationsCoordinator>();
        services.AddSingleton<CollectionOperationsCoordinator>();
        services.AddTransient<HotkeyWindow>();

        // Register orchestration services
        services.AddSingleton<SingleInstanceManager>();
        services.AddSingleton<BackupOrchestrationService>();
        services.AddSingleton<StartupOrchestrationService>();

        // Register initialization pipeline and steps
        services.AddSingleton<StartupInitializationPipeline>();
        services.AddSingleton<IStartupInitializationStep, ConfigurationLoadingStep>();
        services.AddSingleton<IStartupInitializationStep, DatabaseSchemaInitializationStep>();
        services.AddSingleton<IStartupInitializationStep, DatabaseLoadingStep>();
        services.AddSingleton<IStartupInitializationStep, DefaultDataInitializationStep>();
        services.AddSingleton<IStartupInitializationStep, HotkeyInitializationStep>();
        services.AddSingleton<IStartupInitializationStep, HotkeyRegistrationStep>();
        services.AddSingleton<IStartupInitializationStep, ClipOperationsInitializationStep>();

        return services;
    }
}
