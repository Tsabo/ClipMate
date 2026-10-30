namespace ClipMate.App.Services;

/// <summary>
/// Default implementation of <see cref="IActiveWindowService" />.
/// Tracks which window is currently active for event routing and dialog ownership.
/// </summary>
public class ActiveWindowService : IActiveWindowService
{
    /// <inheritdoc />
    public ActiveWindowType ActiveWindow { get; set; } = ActiveWindowType.None;

    private Window? _dialogOwner;

    /// <inheritdoc />
    public Window? DialogOwner
    {
        // The last active window may since have been closed, hidden or minimized to the tray
        get => _dialogOwner?.CanOwnDialogs() == true
            ? _dialogOwner
            : System.Windows.Application.Current?.GetDialogOwner();
        set => _dialogOwner = value;
    }
}
