using ClipMate.Core.Models.Configuration;

namespace ClipMate.Core.Services;

/// <summary>
/// Options controlling HTML to Markdown conversion.
/// </summary>
/// <param name="Flavor">The target Markdown dialect.</param>
/// <param name="RemoveComments">Whether HTML comments are dropped from the output.</param>
/// <param name="SmartHref">Whether links whose text equals the URL are emitted as bare URLs.</param>
public sealed record MarkdownConversionOptions(
    MarkdownFlavor Flavor = MarkdownFlavor.GitHub,
    bool RemoveComments = true,
    bool SmartHref = true);
