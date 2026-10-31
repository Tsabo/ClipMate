using ClipMate.Core.Models;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// The current Paste Target, kept up to date by <see cref="IPasteTargetTracker" />. A change of window is announced
/// with <c>QuickPasteTargetChangedEvent</c> and a change of lock with <c>StateRefreshRequestedEvent</c>; either can
/// arrive on a background thread.
/// </summary>
public interface IPasteTarget
{
    /// <summary>
    /// Gets the window QuickPaste pastes into, or null when there is none.
    /// </summary>
    PasteTargetWindow? Window { get; }

    /// <summary>
    /// Gets whether the target is locked, so switching windows does not change it.
    /// </summary>
    bool IsLocked { get; }
}
