namespace ClipMate.Core.Events;

/// <summary>
/// Event requesting that selected clips be printed.
/// </summary>
/// <param name="ClipIds">The IDs of clips to print.</param>
/// <param name="DatabaseKey">The database key for the clips.</param>
public sealed record PrintRequestedEvent(Guid[] ClipIds, string DatabaseKey);
