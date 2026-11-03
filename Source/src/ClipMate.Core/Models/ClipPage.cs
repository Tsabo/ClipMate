namespace ClipMate.Core.Models;

/// <summary>
/// One page of a clip list: the clips' metadata (no content) at <paramref name="Offset" /> of a list of
/// <paramref name="TotalCount" /> clips, in display order.
/// </summary>
/// <param name="Clips">The clips on this page.</param>
/// <param name="Offset">The position of the first clip on this page in the whole list.</param>
/// <param name="TotalCount">
/// How many clips the whole list has. The list is counted only for its first page (offset 0); a later page counts no
/// further than itself (see <see cref="Uncounted" />), so take the total from the first page.
/// </param>
public sealed record ClipPage(IReadOnlyList<Clip> Clips, int Offset, int TotalCount)
{
    /// <summary>
    /// Gets whether more clips follow this page.
    /// </summary>
    public bool HasMore => Clips.Count > 0 && Offset + Clips.Count < TotalCount;

    /// <summary>
    /// A page whose list was not counted: its <see cref="TotalCount" /> runs to the end of the page, plus one when
    /// <paramref name="hasMore" />, so that <see cref="HasMore" /> still holds.
    /// </summary>
    public static ClipPage Uncounted(IReadOnlyList<Clip> clips, int offset, bool hasMore) =>
        new(clips, offset, offset + clips.Count + (hasMore && clips.Count > 0 ? 1 : 0));
}
