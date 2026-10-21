namespace ClipMate.App.Models;

/// <summary>
/// Drag data for clips dragged out of a Remote Database. Dropping it on a local collection performs Copy to local;
/// it is deliberately a different type from the <c>Clip[]</c> drag data of local clips, which collections move.
/// </summary>
/// <param name="SourceDatabaseKey">The Remote Database routing key of the clips.</param>
/// <param name="ClipIds">The dragged clips.</param>
public sealed record RemoteClipDragData(string SourceDatabaseKey, IReadOnlyList<Guid> ClipIds);
