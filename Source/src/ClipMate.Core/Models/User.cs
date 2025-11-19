namespace ClipMate.Core.Models;

/// <summary>
/// Represents a user/workstation in multi-user scenarios.
/// Matches ClipMate 7.5 users table structure exactly.
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Username (USERNAME in ClipMate 7.5, 50 chars max).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Workstation/computer name (WORKSTATION in ClipMate 7.5, 50 chars max).
    /// </summary>
    public string Workstation { get; set; } = string.Empty;

    /// <summary>
    /// Last activity timestamp (LASTDATE in ClipMate 7.5).
    /// </summary>
    public DateTime LastDate { get; set; }
}
