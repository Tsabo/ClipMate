namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when another collection, folder or Remote Database collection is selected in the collection tree.
/// </summary>
/// <param name="Name">
/// The collection as shown outside the tree (see <see cref="Models.ClipDescription.CollectionOf" />): its name, followed by
/// "on &lt;Peer&gt;" for a Remote Database.
/// </param>
public sealed record CurrentCollectionChangedEvent(string Name);
