using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Manages window state, child view models, and coordinates the three-pane interface.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "ClipMate";

    [ObservableProperty]
    private double _windowWidth = 1200;

    [ObservableProperty]
    private double _windowHeight = 800;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _leftPaneWidth = 250;

    [ObservableProperty]
    private double _rightPaneWidth = 400;

    /// <summary>
    /// Sets the status message displayed in the status bar.
    /// </summary>
    /// <param name="message">The status message to display.</param>
    public void SetStatus(string message)
    {
        StatusMessage = message ?? string.Empty;
    }

    /// <summary>
    /// Sets the busy state and optional status message.
    /// </summary>
    /// <param name="isBusy">Whether the application is busy.</param>
    /// <param name="message">Optional status message to display when busy.</param>
    public void SetBusy(bool isBusy, string? message = null)
    {
        IsBusy = isBusy;
        StatusMessage = isBusy ? (message ?? string.Empty) : string.Empty;
    }

    /// <summary>
    /// Command to open the template management dialog.
    /// This is a relay command that will be wired up in the code-behind.
    /// </summary>
    [RelayCommand]
    private void ManageTemplates()
    {
        // This will be handled in MainWindow code-behind
        // The command is just here to enable keyboard binding
    }
}
