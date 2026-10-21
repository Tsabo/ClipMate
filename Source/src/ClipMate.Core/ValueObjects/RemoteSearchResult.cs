using System.Globalization;
using ClipMate.Core.Models;

namespace ClipMate.Core.ValueObjects;

/// <summary>
/// The results of a Remote Search, kept on the requesting Device: the matching clips' metadata as the Peer returned them
/// (at most 1,000, newest first) and how many clips matched in all. Unlike <see cref="SearchResult" /> it holds the clips
/// themselves, since a Remote Database has no local store to load them from.
/// </summary>
public sealed record RemoteSearchResult
{
    /// <summary>
    /// Creates the result of a Remote Search.
    /// </summary>
    /// <param name="query">A description of what was searched for.</param>
    /// <param name="clips">The matching clips returned by the Peer.</param>
    /// <param name="totalMatches">How many clips matched, including any the Peer did not return.</param>
    public RemoteSearchResult(string query, IReadOnlyList<Clip> clips, int totalMatches)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(clips);

        Query = query;
        Clips = clips;
        TotalMatches = Math.Max(totalMatches, clips.Count);
        SearchedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// A description of what was searched for.
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// The matching clips returned by the Peer (metadata only), newest first.
    /// </summary>
    public IReadOnlyList<Clip> Clips { get; }

    /// <summary>
    /// How many clips matched, including any the Peer did not return.
    /// </summary>
    public int TotalMatches { get; }

    /// <summary>
    /// When the search was run.
    /// </summary>
    public DateTimeOffset SearchedAt { get; }

    /// <summary>
    /// Gets whether more clips matched than the Peer returned.
    /// </summary>
    public bool IsTruncated => TotalMatches > Clips.Count;

    /// <summary>
    /// Describes the result for display, e.g. <c>"report" (showing 1,000 of 4,212)</c>.
    /// </summary>
    public string Describe() => IsTruncated
        ? string.Format(CultureInfo.CurrentCulture, "\"{0}\" (showing {1:N0} of {2:N0})", Query, Clips.Count, TotalMatches)
        : string.Format(CultureInfo.CurrentCulture, "\"{0}\" ({1:N0} results)", Query, Clips.Count);
}
