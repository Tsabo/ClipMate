namespace ClipMate.Core.Events;

/// <summary>
/// Request to deactivate a database (unload it from memory).
/// </summary>
/// <param name="DatabaseKey">The database file path (key) to deactivate.</param>
public record DeactivateDatabaseRequestedEvent(string DatabaseKey);
