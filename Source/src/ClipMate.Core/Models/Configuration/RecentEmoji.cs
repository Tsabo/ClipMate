namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Tracks recently used emojis with usage statistics.
/// </summary>
public class RecentEmoji
{
    /// <summary>
    /// The emoji character(s).
    /// </summary>
    public required string Emoji { get; set; }

    /// <summary>
    /// The last time this emoji was used.
    /// </summary>
    public DateTime LastUsed { get; set; }

    /// <summary>
    /// Number of times this emoji has been used.
    /// </summary>
    public int UseCount { get; set; }
}
