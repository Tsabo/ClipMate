namespace ClipMate.Core.Constants;

/// <summary>
/// Messages shown when an action can't be done to a Database.
/// </summary>
public static class DatabaseMessages
{
    /// <summary>
    /// Shown when an action would change a read-only Database (every Remote Database is read-only).
    /// </summary>
    public const string ReadOnly = "Remote Databases are read-only.";
}
