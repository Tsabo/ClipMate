namespace ClipMate.App.Services;

/// <summary>
/// Default implementation of <see cref="IActiveWindowService" />.
/// Tracks which window is currently active for event routing and dialog ownership.
/// </summary>
public class ActiveWindowService : IActiveWindowService
{
    /// <inheritdoc />
    public ActiveWindowType ActiveWindow { get; set; } = ActiveWindowType.None;

    /// <inheritdoc />
    public Window? DialogOwner { get; set; }
}
