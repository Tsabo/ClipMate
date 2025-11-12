namespace ClipMate.Core.Models;

/// <summary>
/// Defines the sorting mode for text lines.
/// </summary>
public enum SortMode
{
    /// <summary>
    /// Sort lines alphabetically (A-Z).
    /// </summary>
    Alphabetical,

    /// <summary>
    /// Sort lines numerically (1, 2, 10, 21, 100).
    /// </summary>
    Numerical,

    /// <summary>
    /// Reverse the current order of lines.
    /// </summary>
    Reverse
}
