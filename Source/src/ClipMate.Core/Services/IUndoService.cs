namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing single-level undo operations for text editor.
/// Per user manual: undo applies only to text edits and toolbar transformations.
/// Does NOT support clip-level operations (create/delete/move).
/// </summary>
public interface IUndoService
{
    /// <summary>
    /// Gets whether undo is currently available.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Pushes the current text state onto the undo stack.
    /// Only keeps the most recent state (single-level undo).
    /// </summary>
    /// <param name="content">The text content to save for undo.</param>
    void PushState(string? content);

    /// <summary>
    /// Performs undo operation and returns the previous text content.
    /// </summary>
    /// <returns>The previous text content, or null if no undo is available.</returns>
    string? Undo();

    /// <summary>
    /// Clears the undo state.
    /// </summary>
    void Clear();
}
