namespace ClipMate.Core.Models.Search;

/// <summary>
/// The clips a search found (metadata only, newest first), at most a requested number of them, and how many clips
/// matched in all.
/// </summary>
/// <param name="Clips">The matching clips returned, newest first.</param>
/// <param name="TotalMatches">How many clips matched, including any beyond <paramref name="Clips" />.</param>
public sealed record SearchMatches(IReadOnlyList<Clip> Clips, int TotalMatches)
{
    /// <summary>
    /// Gets whether more clips matched than were returned.
    /// </summary>
    public bool IsTruncated => TotalMatches > Clips.Count;
}
