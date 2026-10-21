using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when a node in a Remote Database (the database itself, a collection or a folder) is selected
/// in the collection tree. Unlike <see cref="CollectionNodeSelectedEvent" />, it never changes the active collection,
/// so a Remote Database never becomes the capture target.
/// </summary>
/// <param name="Address">The Remote Database.</param>
/// <param name="CollectionId">The selected collection, or <c>null</c> when the Remote Database node itself is selected.</param>
/// <param name="FolderId">The selected folder, or <c>null</c> for a collection.</param>
public sealed record RemoteDatabaseNodeSelectedEvent(RemoteDatabaseAddress Address, Guid? CollectionId, Guid? FolderId);
