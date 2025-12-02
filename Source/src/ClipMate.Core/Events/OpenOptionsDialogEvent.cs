namespace ClipMate.Core.Events;

/// <summary>
/// Event published to request opening the Options dialog.
/// </summary>
public class OpenOptionsDialogEvent
{
    /// <summary>
    /// Gets the name of the tab to select when opening the dialog.
    /// </summary>
    public string? TabName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenOptionsDialogEvent" /> class.
    /// </summary>
    /// <param name="tabName">Optional name of the tab to select.</param>
    public OpenOptionsDialogEvent(string? tabName = null)
    {
        TabName = tabName;
    }
}
