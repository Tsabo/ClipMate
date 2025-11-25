using ClipMate.Core.Services;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for TextTransformService text manipulation functionality.
/// User Story 6: Text Processing Tools
/// </summary>
public partial class TextTransformServiceTests
{
    private readonly TextTransformService _service;

    public TextTransformServiceTests()
    {
        _service = new TextTransformService();
    }
}
