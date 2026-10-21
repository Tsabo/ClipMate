using ClipMate.Core.Models.Networking;
using ClipMate.Core.ValueObjects;

namespace ClipMate.Core.Events;

/// <summary>
/// Messenger event sent when a Remote Database's Search Results node is selected, or its results change while it is
/// selected: the clip list shows <paramref name="Result" /> read-only. Like <see cref="RemoteDatabaseNodeSelectedEvent" />,
/// it never changes the active collection.
/// </summary>
/// <param name="Address">The Remote Database that was searched.</param>
/// <param name="Result">The Remote Search results, as cached on this Device.</param>
public sealed record RemoteSearchResultsSelectedEvent(RemoteDatabaseAddress Address, RemoteSearchResult Result);
