namespace ClipMate.Core.Events;

/// <summary>
/// Request to activate a database (load it into memory).
/// </summary>
/// <param name="DatabaseKey">The database file path (key) to activate.</param>
public record ActivateDatabaseRequestedEvent(string DatabaseKey);
