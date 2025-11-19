using System.Windows;
using System.Windows.Input;

namespace ClipMate.App;

/// <summary>
/// Hidden window that hosts the system tray icon service.
/// </summary>
public partial class TrayIconWindow : Window
{
    public ICommand ShowMainWindowCommand { get; }
    public ICommand ExitApplicationCommand { get; }

    public TrayIconWindow()
    {
        InitializeComponent();
        
        ShowMainWindowCommand = new RelayCommand(ShowMainWindowExecute);
        ExitApplicationCommand = new RelayCommand(ExitApplicationExecute);
        
        DataContext = this;
    }

    private void ShowMainWindowExecute(object? parameter)
    {
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }
    }

    private void ExitApplicationExecute(object? parameter)
    {
        System.Windows.Application.Current.Shutdown();
    }

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

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }
}
