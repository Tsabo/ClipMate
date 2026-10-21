namespace ClipMate.Core.Events;

/// <summary>
/// Requests Copy to local: storing clips of a Remote Database as new, independent clips in a local collection.
/// Sent by the clip list context menu (no target: the user picks a local collection) and by dropping remote clips on
/// a local collection (target given). Never a move: the Remote Database is never changed.
/// </summary>
/// <param name="ClipIds">The clips to copy; empty for the clips selected in the clip list.</param>
/// <param name="SourceDatabaseKey">The Remote Database routing key of the clips; <c>null</c> for the Database the clip list shows.</param>
/// <param name="TargetDatabaseKey">The local Database to copy into; <c>null</c> to ask the user.</param>
/// <param name="TargetCollectionId">The local collection to copy into; <c>null</c> to ask the user.</param>
public record CopyToLocalRequestedEvent(
    IReadOnlyList<Guid> ClipIds,
    string? SourceDatabaseKey = null,
    string? TargetDatabaseKey = null,
    Guid? TargetCollectionId = null);
