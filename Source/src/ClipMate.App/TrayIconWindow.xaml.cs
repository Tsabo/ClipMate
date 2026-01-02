using System.Windows.Input;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Application = System.Windows.Application;

namespace ClipMate.App;

/// <summary>
/// Hidden window that hosts the system tray icon service.
/// </summary>
public partial class TrayIconWindow : IRecipient<ShowTrayIconChangedEvent>, IRecipient<IconClickBehaviorChangedEvent>
{
    private readonly IConfigurationService _configurationService;
    private readonly IMessenger _messenger;

    public TrayIconWindow(
        IConfigurationService configurationService,
        IMessenger messenger,
        MainMenuViewModel mainMenu)
    {
        InitializeComponent();

        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        MainMenu = mainMenu ?? throw new ArgumentNullException(nameof(mainMenu));

        ShowExplorerWindowCommand = new RelayCommand(ShowExplorerWindowExecute);
        ExitApplicationCommand = new RelayCommand(ExitApplicationExecute);

        DataContext = this;

        // Register for configuration change events
        _messenger.Register<ShowTrayIconChangedEvent>(this);
        _messenger.Register<IconClickBehaviorChangedEvent>(this);
    }

    /// <summary>
    /// Gets the shared main menu view model for binding menu commands.
    /// </summary>
    public MainMenuViewModel MainMenu { get; }

    public ICommand ShowExplorerWindowCommand { get; }
    public ICommand ExitApplicationCommand { get; }

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

    private void ShowExplorerWindowExecute(object? parameter)
    {
        // Check configuration for left-click action
        var action = _configurationService.Configuration.Preferences.TrayIconLeftClickAction;

        if (action == IconLeftClickAction.ShowClipBar)
        {
            // Send event to show ClipBar popup
            _messenger.Send(new ShowClipBarRequestedEvent());
        }
        else
        {
            // Send event to show main window (default behavior)
            _messenger.Send(new ShowExplorerWindowEvent());
        }
    }

    private void ExitApplicationExecute(object? parameter) => Application.Current.Shutdown();

    /// <summary>
    /// Simple relay command implementation.
    /// </summary>
    private class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;

        public RelayCommand(Action<object?> execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

#pragma warning disable CS0067 // Event is required by ICommand interface but never raised in this simple implementation
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}
