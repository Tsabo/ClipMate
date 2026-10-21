namespace ClipMate.Core.Models.Search;

/// <summary>
/// Limits of Remote Search, applied by the Peer that runs it.
/// </summary>
public static class RemoteSearchLimits
{
    /// <summary>
    /// The most matching clips a Peer returns for one Remote Search; the total number of matches is reported as well.
    /// </summary>
    public const int MaxResults = 1000;

    /// <summary>
    /// The longest a Peer lets one Remote Search run before stopping it.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
}
