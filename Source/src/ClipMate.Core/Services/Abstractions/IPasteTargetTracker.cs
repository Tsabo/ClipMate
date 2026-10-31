namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Auto-targeting: follows the foreground window and keeps <see cref="IPasteTarget" /> up to date. The Paste Target is
/// the last suitable window that is not ClipMate, updated as the user switches windows unless the target is locked.
/// When ClipMate comes to the front and that window has closed or is hidden, the next suitable window down the window
/// stack takes its place.
/// </summary>
public interface IPasteTargetTracker
{
    /// <summary>
    /// Starts following the foreground window. Calling it again has no effect.
    /// </summary>
    void Start();

    /// <summary>
    /// Locks or unlocks the Paste Target. Unlocking catches up with the last suitable window the user switched to
    /// while it was locked.
    /// </summary>
    void SetLocked(bool locked);
}
