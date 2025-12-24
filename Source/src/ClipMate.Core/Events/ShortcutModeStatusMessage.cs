namespace ClipMate.Core.Events;

/// <summary>
/// Message sent when shortcut mode status changes.
/// Used to update the status bar with shortcut filtering information.
/// </summary>
public sealed class ShortcutModeStatusMessage
{
    public ShortcutModeStatusMessage(bool isActive, string filter, int matchCount)
    {
        IsActive = isActive;
        Filter = filter;
        MatchCount = matchCount;
    }

    /// <summary>
    /// Whether shortcut mode is currently active.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// The current shortcut filter string (e.g., ".cc", ".cc.v").
    /// </summary>
    public string Filter { get; }

    /// <summary>
    /// The number of shortcuts matching the current filter.
    /// </summary>
    public int MatchCount { get; }
}
