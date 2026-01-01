namespace ClipMate.Core.Events;

/// <summary>
/// Event published when the outbound clip filter is toggled.
/// When enabled, clipboard contents are replaced with plain-text version after capture.
/// </summary>
public class OutboundFilterToggleEvent
{
    /// <summary>
    /// Gets a value indicating whether the outbound filter is enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundFilterToggleEvent"/> class.
    /// </summary>
    /// <param name="isEnabled">Whether the filter should be enabled.</param>
    public OutboundFilterToggleEvent(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
