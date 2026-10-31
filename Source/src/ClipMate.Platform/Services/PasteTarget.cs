using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Platform.Services;

/// <summary>
/// <see cref="IPasteTarget" />, updated only by <see cref="PasteTargetTracker" />. Readers depend on this small state
/// object instead of the tracker and its thread.
/// </summary>
public sealed class PasteTarget : IPasteTarget
{
    private volatile bool _isLocked;
    private volatile PasteTargetWindow? _window;

    /// <inheritdoc />
    public PasteTargetWindow? Window => _window;

    /// <inheritdoc />
    public bool IsLocked => _isLocked;

    internal void SetWindow(PasteTargetWindow? window) => _window = window;

    internal void SetLocked(bool locked) => _isLocked = locked;
}
