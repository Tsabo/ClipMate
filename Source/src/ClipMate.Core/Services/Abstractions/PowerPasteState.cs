namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Represents the state of Power Paste.
/// </summary>
public enum PowerPasteState
{
    /// <summary>
    /// Power Paste is not active.
    /// </summary>
    Inactive,

    /// <summary>
    /// Power Paste is active and waiting for paste operations.
    /// </summary>
    Active,
}
