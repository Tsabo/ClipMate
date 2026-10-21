namespace ClipMate.Core.Models;

/// <summary>
/// One page of a clip list: the clips' metadata (no content) at <paramref name="Offset" /> of a list of
/// <paramref name="TotalCount" /> clips, in display order.
/// </summary>
/// <param name="Clips">The clips on this page.</param>
/// <param name="Offset">The position of the first clip on this page in the whole list.</param>
/// <param name="TotalCount">How many clips the whole list has.</param>
public sealed record ClipPage(IReadOnlyList<Clip> Clips, int Offset, int TotalCount)
{
    /// <summary>
    /// Gets whether more clips follow this page.
    /// </summary>
    public bool HasMore => Clips.Count > 0 && Offset + Clips.Count < TotalCount;
}
