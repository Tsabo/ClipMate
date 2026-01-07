using System.ComponentModel;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Bars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App;

/// <summary>
/// Hidden window that hosts the system tray icon service.
/// </summary>
public partial class TrayIconWindow : IRecipient<ShowTrayIconChangedEvent>, IRecipient<IconClickBehaviorChangedEvent>, IRecipient<StateRefreshRequestedEvent>
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<TrayIconWindow> _logger;
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;

    public TrayIconWindow(IServiceProvider serviceProvider,
        ILogger<TrayIconWindow> logger,
        IConfigurationService configurationService,
        IMessenger messenger,
        MainMenuViewModel mainMenu)
    {
        InitializeComponent();

        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        MainMenu = mainMenu ?? throw new ArgumentNullException(nameof(mainMenu));

        ShowExplorerWindowCommand = new RelayCommand(ShowExplorerWindowExecute);
        ExitApplicationCommand = new RelayCommand(ExitApplicationExecute);

        DataContext = this;

        // Explicitly set the TrayContextMenu DataContext after initialization
        // NotifyIconService PopupMenu doesn't inherit DataContext automatically
        Loaded += (_, _) => TrayContextMenu?.DataContext = this;

        // Register for configuration change events
        _messenger.Register<ShowTrayIconChangedEvent>(this);
        _messenger.Register<IconClickBehaviorChangedEvent>(this);
        _messenger.Register<StateRefreshRequestedEvent>(this);
    }

    /// <summary>
    /// Gets the shared main menu view model for binding menu commands.
    /// </summary>
    public MainMenuViewModel MainMenu { get; }

    public RelayCommand ShowExplorerWindowCommand { get; }
    public RelayCommand ExitApplicationCommand { get; }

    public void Receive(IconClickBehaviorChangedEvent message)
    {
        // Icon click behavior is checked dynamically in ShowExplorerWindowExecute
        // No action needed here as configuration is always read fresh
    }

    public void Receive(ShowTrayIconChangedEvent message)
    {
        // Note: DevExpress NotifyIconService visibility is controlled by the window itself
        // The App.xaml.cs handles creation/destruction of this window based on ShowTrayIcon setting
        // This method is here for future extensibility if runtime show/hide is needed
    }

    public void Receive(StateRefreshRequestedEvent message)
    {
        // The context menu bindings will be refreshed when the menu opens via the Opening event.
        // We don't need to do anything here since the PopupMenu will re-evaluate bindings
        // each time it's shown.
    }

    private void TrayContextMenu_Opening(object sender, CancelEventArgs e)
    {
        // Ensure DataContext is set and force re-evaluation of all bindings when the context menu opens
        // This ensures Auto Capture and other state-dependent menu items show current values
        if (sender is not PopupMenu menu)
            return;

        // Ensure the DataContext is set (in case it wasn't inherited)
        menu.DataContext ??= this;

        // Force binding refresh by clearing and restoring DataContext
        var currentContext = menu.DataContext;
        menu.DataContext = null;
        menu.DataContext = currentContext;
    }

    private void ShowExplorerWindowExecute()
    {
        // Check configuration for left-click action
        var action = _configurationService.Configuration.Preferences.TrayIconLeftClickAction;

        if (action == IconLeftClickAction.ShowClassWindow)
        {
            if (!Application.Current.Windows.OfType<ClassicWindow>().Any())
            {
                _ = _serviceProvider.GetRequiredService<ClassicWindow>();
                _logger.LogDebug("Created Classic window");
            }

            // Send event to show ClipBar popup
            _messenger.Send(new ShowClipBarRequestedEvent());
        }
        else
        {
            if (!Application.Current.Windows.OfType<ExplorerWindow>().Any())
            {
                _ = _serviceProvider.GetRequiredService<ExplorerWindow>();
                _logger.LogDebug("Created Explorer window");
            }

            // Send event to show main window (default behavior)
            _messenger.Send(new ShowExplorerWindowEvent());
        }
    }

    private void ExitApplicationExecute() => Application.Current.Shutdown();
}
