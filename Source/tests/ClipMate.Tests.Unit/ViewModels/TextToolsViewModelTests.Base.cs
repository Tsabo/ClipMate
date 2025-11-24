using ClipMate.App.ViewModels;
using ClipMate.Core.Services;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for TextToolsViewModel.
/// User Story 6: Text Processing Tools
/// </summary>
public partial class TextToolsViewModelTests
{
    private readonly TextTransformService _textTransformService;
    private readonly TextToolsViewModel _viewModel;

    public TextToolsViewModelTests()
    {
        _textTransformService = new TextTransformService();
        _viewModel = new TextToolsViewModel(_textTransformService);
    }
}
