namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Target Markdown dialect used when converting HTML/RTF clips to Markdown.
/// </summary>
public enum MarkdownFlavor
{
    /// <summary>
    /// Standard Markdown.
    /// </summary>
    Default,

    /// <summary>
    /// GitHub Flavored Markdown (tables, fenced code, strikethrough).
    /// </summary>
    GitHub,

    /// <summary>
    /// Slack mrkdwn (*bold*, _italic_, ~strike~).
    /// </summary>
    Slack,

    /// <summary>
    /// Telegram MarkdownV2.
    /// </summary>
    Telegram,

    /// <summary>
    /// MultiMarkdown.
    /// </summary>
    MultiMarkdown,

    /// <summary>
    /// Pandoc Markdown.
    /// </summary>
    Pandoc,
}
