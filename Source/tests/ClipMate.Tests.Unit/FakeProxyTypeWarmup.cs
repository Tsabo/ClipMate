using System.Reflection;
using ClipMate.App.Services;
using ClipMate.App.Services.Initialization;
using ClipMate.App.ViewModels;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Networking.Discovery.Abstraction;
using ClipMate.Platform;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit;

/// <summary>
/// FakeItEasy generates a Castle.DynamicProxy proxy TYPE the first time each distinct type is
/// faked; that generation is not thread-safe (github.com/FakeItEasy/FakeItEasy/issues/1794) and
/// crashes the test process with a native heap corruption when many different types are
/// first-faked concurrently under TUnit's default parallel execution.
///
/// This hook pre-generates every type this project passes to A.Fake&lt;T&gt;(), once, sequentially,
/// before the parallel test session starts, so the race never happens and tests can stay parallel
/// instead of the whole project being forced sequential.
///
/// If a new type is passed to A.Fake&lt;T&gt;() in a test and isn't in the list below, add it here too -
/// otherwise that type's first use during a parallel run is exposed to the same race again.
/// </summary>
public static class FakeProxyTypeWarmup
{
    private static readonly Type[] Types =
    [
        typeof(ClipListViewModel),
        typeof(CollectionTreeViewModel),
        typeof(HotkeyCoordinator),
        typeof(IActiveWindowService),
        typeof(IApplicationFilterRepository),
        typeof(IApplicationFilterService),
        typeof(IApplicationProfileService),
        typeof(IApplicationProfileStore),
        typeof(IAutoAppendService),
        typeof(IBlobRepository),
        typeof(IClipAppendService),
        typeof(IClipDataRepository),
        typeof(IClipRepository),
        typeof(IClipService),
        typeof(IClipContentService),
        typeof(IClipViewerWindowManager),
        typeof(IClipPopupNotifications),
        typeof(DevExpress.Mvvm.INotification),
        typeof(IClipboardFormatEnumerator),
        typeof(IClipboardService),
        typeof(ICollectionRepository),
        typeof(ICollectionService),
        typeof(ICollectionTreeBuilder),
        typeof(IConfigurationService),
        typeof(IDatabaseContextFactory),
        typeof(IDatabaseMaintenanceService),
        typeof(IDatabaseManager),
        typeof(IDatabaseIdService),
        typeof(IDecryptedBlobCacheService),
        typeof(IDialogService),
        typeof(IEncryptionService),
        typeof(IFolderRepository),
        typeof(IFolderService),
        typeof(IHotkeyManager),
        typeof(IHotkeyService),
        typeof(IDeviceIdentityService),
        typeof(IFirewallRuleService),
        typeof(INetworkProfileService),
        typeof(IClipMate7ImportService),
        typeof(IMarkdownConversionService),
        typeof(IRtfToHtmlConverter),
        typeof(INetshRunner),
        typeof(IPasteTarget),
        typeof(IPasteTargetTracker),
        typeof(IPeerHost),
        typeof(IPeerHostFactory),
        typeof(IPeerStore),
        typeof(IPeerStatusService),
        typeof(ILocalDevice),
        typeof(ISharedDatabaseService),
        typeof(ISharedDatabaseChangeFeed),
        typeof(IRemoteDatabaseService),
        typeof(IRemoteDatabaseTreeBuilder),
        typeof(ClipMate.Networking.SharedDatabases.ISharedDatabaseClient),
        typeof(IPairingDialogService),
        typeof(IPeerDialogService),
        typeof(IUnpairingService),
        typeof(IPairingSession),
        typeof(ILocalDevice),
        typeof(IDeviceDiscoveryService),
        typeof(IDiscoveryTransportFactory),
        typeof(ILogger<NetworkingService>),
        typeof(ILogger<DevicesOptionsViewModel>),
        typeof(ILogger<AboutDialogViewModel>),
        typeof(ILogger<AdvancedOptionsViewModel>),
        typeof(ILogger<AppearanceOptionsViewModel>),
        typeof(ILogger<ApplicationFilterService>),
        typeof(ILogger<ApplicationProfileService>),
        typeof(ILogger<ApplicationProfileStore>),
        typeof(ILogger<ApplicationProfilesOptionsViewModel>),
        typeof(ILogger<CapturingOptionsViewModel>),
        typeof(ILogger<ClassicWindowCoordinator>),
        typeof(ILogger<ClipAppendService>),
        typeof(ILogger<ClipListViewModel>),
        typeof(ILogger<ClipOperationsCoordinator>),
        typeof(ILogger<ClipService>),
        typeof(ILogger<ClipViewerViewModel>),
        typeof(ILogger<ClipboardDiagnosticsService>),
        typeof(ILogger<CollectionOperationsCoordinator>),
        typeof(ILogger<CollectionTreeViewModel>),
        typeof(ILogger<DatabaseContextFactory>),
        typeof(ILogger<DatabaseMaintenanceService>),
        typeof(ILogger<DatabaseManager>),
        typeof(ILogger<DatabaseOptionsViewModel>),
        typeof(ILogger<DecryptedBlobCacheService>),
        typeof(ILogger<EditorOptionsViewModel>),
        typeof(ILogger<ExplorerWindowViewModel>),
        typeof(ILogger<GeneralOptionsViewModel>),
        typeof(ILogger<HotkeyCoordinator>),
        typeof(ILogger<HotkeyService>),
        typeof(ILogger<IRetentionEnforcementService>),
        typeof(ILogger<MaintenanceSchedulerService>),
        typeof(ILogger<OptionsViewModel>),
        typeof(ILogger<PasteService>),
        typeof(ILogger<PasteTargetTracker>),
        typeof(ILogger<PowerPasteClipboardBridge>),
        typeof(ILogger<PowerPasteOptionsViewModel>),
        typeof(ILogger<PowerPasteService>),
        typeof(ILogger<PrintService>),
        typeof(ILogger<QuickPasteOptionsViewModel>),
        typeof(ILogger<QuickPasteService>),
        typeof(ILogger<QuickPasteToolbarViewModel>),
        typeof(ILogger<SearchService>),
        typeof(ILogger<SessionMonitorService>),
        typeof(ILogger<SetupService>),
        typeof(ILogger<ShortcutService>),
        typeof(ILogger<SoundService>),
        typeof(ILogger<SoundsOptionsViewModel>),
        typeof(ILogger<SqlMaintenanceService>),
        typeof(ILogger<SqlValidationService>),
        typeof(ILogger<StartupInitializationPipeline>),
        typeof(ILogger<StartupManager>),
        typeof(ILogger<TemplateService>),
        typeof(ILogger<UpdateCheckService>),
        typeof(IMacroExecutionService),
        typeof(IMessenger),
        typeof(IPowerPasteService),
        typeof(IProgress<string>),
        typeof(IQuickPasteService),
        typeof(IRetentionEnforcementService),
        typeof(ISearchService),
        typeof(ISearchTargetSource),
        typeof(IServiceProvider),
        typeof(IServiceScope),
        typeof(IServiceScopeFactory),
        typeof(ISoundService),
        typeof(ISqlValidationService),
        typeof(IStartupInitializationStep),
        typeof(IStartupManager),
        typeof(ITemplateService),
        typeof(IUndoService),
        typeof(IUpdateCheckService),
        typeof(IWin32ClipboardInterop),
        typeof(IWin32HotkeyInterop),
        typeof(IWin32IdleDetector),
        typeof(IWin32InputInterop),
    ];

    [Before(TestSession)]
    public static void WarmUpFakeProxyTypes()
    {
        var fakeMethod = typeof(A).GetMethods()
            .First(m => m.Name == nameof(A.Fake) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

        foreach (var type in Types)
        {
            try
            {
                fakeMethod.MakeGenericMethod(type).Invoke(null, null);
            }
            catch
            {
                // Some concrete classes need specific constructor arguments to construct
                // successfully; the proxy TYPE is generated and cached by Castle.DynamicProxy
                // before construction runs, which is all this warmup needs.
            }
        }
    }
}
