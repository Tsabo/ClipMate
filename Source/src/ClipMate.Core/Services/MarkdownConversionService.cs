using AngleSharp.Dom;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ReverseMarkdown;
using ReverseMarkdown.Preprocessing;

namespace ClipMate.Core.Services;

/// <summary>
/// Converts HTML to Markdown using ReverseMarkdown.
/// </summary>
public class MarkdownConversionService : IMarkdownConversionService
{
    /// <inheritdoc />
    public string ConvertHtmlToMarkdown(string html, MarkdownConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var converter = new Converter(CreateConfig(options));

        return converter.Convert(html).Trim();
    }

    private static Config CreateConfig(MarkdownConversionOptions options)
    {
        var config = CreateBaseConfig(options.Flavor);
        config.Links.SmartHref = options.SmartHref;
        config.Formatting.RemoveComments = options.RemoveComments;

        config.Preprocess
            // RTF exports and Office HTML express bold/italic via inline styles; convert before styles are removed
            .ConvertInlineStylesToTags()
            .RemoveScripts()
            .RemoveStyles();

        // Not every flavor honors Formatting.RemoveComments, so strip them from the DOM as well
        if (options.RemoveComments)
            config.Preprocess.RemoveComments();

        // Slack mrkdwn has no tables and ReverseMarkdown throws on them; render them as code blocks like Telegram does
        if (options.Flavor == MarkdownFlavor.Slack)
            config.Preprocess.Add(new TableToPreformattedTextStep());

        return config;
    }

    /// <summary>
    /// Creates the ReverseMarkdown configuration for a flavor with the settings shared by all conversions.
    /// </summary>
    /// <remarks>
    /// GitHub is selected through <see cref="Config.GithubFlavored" /> instead of
    /// <see cref="Config.MarkdownFlavor.GitHub" />: in ReverseMarkdown 6.2.1 the CommonMark-based flavors
    /// emit the input as raw HTML when it starts with a block element such as a heading or table.
    /// </remarks>
    private static Config CreateBaseConfig(MarkdownFlavor flavor) => new()
    {
        Flavor = flavor switch
        {
            MarkdownFlavor.Default => Config.MarkdownFlavor.Default,
            MarkdownFlavor.GitHub => Config.MarkdownFlavor.Default,
            MarkdownFlavor.Slack => Config.MarkdownFlavor.Slack,
            MarkdownFlavor.Telegram => Config.MarkdownFlavor.Telegram,
            MarkdownFlavor.MultiMarkdown => Config.MarkdownFlavor.MultiMarkdown,
            MarkdownFlavor.Pandoc => Config.MarkdownFlavor.Pandoc,
            var _ => throw new ArgumentOutOfRangeException(nameof(flavor), flavor, "Unknown Markdown flavor"),
        },
        GithubFlavored = flavor == MarkdownFlavor.GitHub,
        // Emit Markdown syntax (**bold**, [link](url)) rather than inline HTML tags
        CommonMarkUseHtmlInlineTags = false,
        // Clipboard HTML is full of spans/fonts/etc.; keep their text but drop the tags
        Tags = { Unknown = Config.UnknownTagsOption.Bypass },
        // Base64 images would flood the editor with megabytes of data
        Images = { Base64Handling = Config.Base64ImageHandling.Skip },
        Formatting =
        {
            CleanupSpaces = true,
            OutputLineEnding = Environment.NewLine,
        },
    };

    /// <summary>
    /// Replaces each top-level table with a &lt;pre&gt; block holding the table as a GitHub pipe table.
    /// </summary>
    private sealed class TableToPreformattedTextStep : IHtmlPreprocessStep
    {
        public void Apply(IElement root)
        {
            var tables = root.QuerySelectorAll("table")
                .Where(p => p.ParentElement?.Closest("table") == null)
                .ToList();

            if (tables.Count == 0)
                return;

            var tableConverter = new Converter(CreateBaseConfig(MarkdownFlavor.GitHub));

            foreach (var item in tables)
            {
                var preformatted = item.Owner!.CreateElement("pre");
                preformatted.TextContent = tableConverter.Convert(item.OuterHtml).Trim();
                item.Replace(preformatted);
            }
        }
    }
}
