using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Events;
using CommunityToolkit.Mvvm.Messaging;

namespace ClipMate.App.Services;

/// <summary>
/// Singleton service for managing ClipViewerWindow instance.
/// Subscribes to ClipSelectedEvent for auto-follow functionality.
/// </summary>
public class ClipViewerWindowManager : IClipViewerWindowManager, IRecipient<ClipSelectedEvent>
{
    private readonly IMessenger _messenger;
    private readonly Func<ClipViewerViewModel> _viewModelFactory;

    // Track last selected clip for ToggleVisibility
    private Guid? _lastSelectedClipId;
    private string? _lastSelectedDatabaseKey;
    private ClipViewerWindow? _window;

    public ClipViewerWindowManager(Func<ClipViewerViewModel> viewModelFactory, IMessenger messenger)
    {
        _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        // Register to receive clip selection events
        _messenger.Register(this);
    }

    /// <inheritdoc />
    public bool IsOpen => _window?.IsVisible == true;

    /// <inheritdoc />
    public bool IsPinned { get; set; }

    /// <inheritdoc />
    public void ShowClipViewer(Guid clipId, string? databaseKey = null)
    {
        EnsureWindow();
        _window!.LoadAndShow(clipId, databaseKey);
    }

    /// <inheritdoc />
    public void UpdateClipViewer(Guid clipId, string? databaseKey = null)
    {
        if (_window == null || !IsOpen)
            return;

        _window.UpdateClip(clipId, databaseKey);
    }

    /// <inheritdoc />
    public void ToggleVisibility()
    {
        // If window is open, hide it
        if (IsOpen)
        {
            CloseClipViewer();
            return;
        }

        // Otherwise, show with current selection and clear pin
        IsPinned = false;

        if (_lastSelectedClipId.HasValue)
            ShowClipViewer(_lastSelectedClipId.Value, _lastSelectedDatabaseKey);
        else
        {
            EnsureWindow();
            _window?.Show();
            _window?.Activate();
        }
    }

    /// <inheritdoc />
    public void CloseClipViewer()
    {
        _window?.Hide();
        // Reset pin state when window is closed
        IsPinned = false;
    }

    /// <summary>
    /// Handles clip selection events for auto-follow functionality.
    /// Updates the floating viewer when visible and not pinned.
    /// </summary>
    public void Receive(ClipSelectedEvent message)
    {
        // Always track the last selection for ToggleVisibility
        _lastSelectedClipId = message.SelectedClip?.Id;
        _lastSelectedDatabaseKey = message.DatabaseKey;

        // Skip update if pinned, window not visible, or no clip selected
        if (IsPinned || !IsOpen || message.SelectedClip == null)
            return;

        // Use UpdateClipViewer to avoid stealing focus during auto-follow
        UpdateClipViewer(message.SelectedClip.Id, message.DatabaseKey);
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;

        _window = new ClipViewerWindow(_viewModelFactory(), this);
        _window.Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Reset pin state when window is closed
        IsPinned = false;
    }
}
