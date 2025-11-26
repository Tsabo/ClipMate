using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Base class for TemplateEditorViewModel tests containing shared setup and helper methods.
/// Tests cover: property changes, CRUD commands, validation, preview functionality, and service integration.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    private readonly Mock<ITemplateService> _mockTemplateService;

    public TemplateEditorViewModelTests()
    {
        _mockTemplateService = new Mock<ITemplateService>();
        
        // Default setup for ExtractVariables to return empty list
        _mockTemplateService.Setup(p => p.ExtractVariables(It.IsAny<string>()))
            .Returns(new List<string>());
    }

    private TemplateEditorViewModel CreateViewModel()
    {
        return new TemplateEditorViewModel(_mockTemplateService.Object);
    }
}
