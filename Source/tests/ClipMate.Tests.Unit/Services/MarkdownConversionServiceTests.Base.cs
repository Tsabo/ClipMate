using ClipMate.Core.Services;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for MarkdownConversionService HTML to Markdown conversion.
/// </summary>
public partial class MarkdownConversionServiceTests
{
    private readonly MarkdownConversionService _service;

    public MarkdownConversionServiceTests()
    {
        _service = new MarkdownConversionService();
    }
}
