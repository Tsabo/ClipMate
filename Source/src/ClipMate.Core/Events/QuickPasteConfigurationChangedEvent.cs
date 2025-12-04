namespace ClipMate.Core.Events;

/// <summary>
/// Event published when QuickPaste configuration has changed.
/// Services should subscribe to this event to reload their configuration immediately.
/// </summary>
public class QuickPasteConfigurationChangedEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuickPasteConfigurationChangedEvent" /> class.
    /// </summary>
    public QuickPasteConfigurationChangedEvent()
    {
    }
}
