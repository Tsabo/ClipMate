using ClipMate.Core.Models.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the DatabaseRestoreWizard.
/// </summary>
public partial class RestoreWizardViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcomePage))]
    [NotifyPropertyChangedFor(nameof(IsConfirmationPage))]
    [NotifyPropertyChangedFor(nameof(IsCompletionPage))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(ShowFinish))]
    private int _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(ShowFinish))]
    private bool _isRestoring;

    [ObservableProperty]
    private string _backupZipPath = string.Empty;

    [ObservableProperty]
    private string _progressMessage = string.Empty;

    public RestoreWizardViewModel(DatabaseConfiguration databaseConfig)
    {
        DatabaseConfig = databaseConfig;
    }

    public DatabaseConfiguration DatabaseConfig { get; }

    public string DatabaseName => DatabaseConfig.Name;

    public bool IsWelcomePage => CurrentPage == 0;
    public bool IsConfirmationPage => CurrentPage == 1;
    public bool IsCompletionPage => CurrentPage == 2;

    public bool CanGoBack => CurrentPage > 0 && !IsRestoring;
    public bool CanGoNext => CurrentPage < 2 && !IsRestoring;
    public bool ShowFinish => CurrentPage == 1 && !IsRestoring;
}
