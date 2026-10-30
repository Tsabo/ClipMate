namespace ClipMate.App.Services;

/// <summary>
/// Service to track which window is currently active.
/// Used to ensure only the active window handles shared events like export commands,
/// and to provide the correct dialog owner for modal dialogs.
/// </summary>
public interface IActiveWindowService
{
    /// <summary>
    /// Gets or sets the type of the currently active window.
    /// </summary>
    ActiveWindowType ActiveWindow { get; set; }

    /// <summary>
    /// Gets or sets the currently active window for dialog ownership.
    /// Modal dialogs should use this as their Owner property.
    /// Null when no ClipMate window can own a dialog, e.g. when ClipMate is in the tray; such a dialog should be
    /// placed with <see cref="WindowExtensions.CenterOnOwnerOrScreen" />.
    /// </summary>
    Window? DialogOwner { get; set; }
}
