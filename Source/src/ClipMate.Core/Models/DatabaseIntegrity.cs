namespace ClipMate.Core.Models;

/// <summary>
/// The result of checking a Database file's integrity.
/// </summary>
public enum DatabaseIntegrity
{
    /// <summary>
    /// The file opened and passed the integrity check.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// The file is damaged or is not a database, so a repair may help.
    /// </summary>
    Corrupt = 1,

    /// <summary>
    /// The file is missing or cannot be opened (for example its drive is not connected). Nothing is known about its
    /// contents, so it is not corrupt and a repair cannot help.
    /// </summary>
    Unavailable = 2,
}
