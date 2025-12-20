using ClipMate.Core.Services;

namespace ClipMate.Data.Services;

/// <summary>
/// Implementation of single-level undo service for text editor operations.
/// Maintains only one previous state - undo returns to that state once.
/// </summary>
public class UndoService : IUndoService
{
    private string? _previousState;

    /// <inheritdoc />
    public bool CanUndo => _previousState != null;

    /// <inheritdoc />
    public void PushState(string? content)
    {
        // Only save non-null content
        if (content != null)
            _previousState = content;
    }

    /// <inheritdoc />
    public string? Undo()
    {
        var state = _previousState;
        _previousState = null; // Single-level: clear after returning
        return state;
    }

    /// <inheritdoc />
    public void Clear() => _previousState = null;
}
