namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when preferences/configuration has been updated.
/// Allows services and components to react to configuration changes without direct coupling.
/// </summary>
public class PreferencesChangedEvent
{
    /// <summary>
    /// Creates a new preferences changed event.
    /// </summary>
    public PreferencesChangedEvent()
    {
    }
}
