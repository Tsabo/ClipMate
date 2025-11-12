using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.Core.ViewModels;

/// <summary>
/// Base class for all view models in the application.
/// Provides property change notification and common functionality.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _busyMessage;

    /// <summary>
    /// Gets or sets a value indicating whether the view model is currently busy.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// Gets or sets the message to display while busy.
    /// </summary>
    public string? BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    /// <summary>
    /// Called when the view model is activated/loaded.
    /// Override this method to perform initialization logic.
    /// </summary>
    public virtual Task OnActivatedAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the view model is deactivated/unloaded.
    /// Override this method to perform cleanup logic.
    /// </summary>
    public virtual Task OnDeactivatedAsync()
    {
        return Task.CompletedTask;
    }
}
