using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for TemplateEditorViewModel variable extraction.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task ExtractedVariables_ShouldUpdateWhenContentChanges()
    {
        // Arrange
        _mockTemplateService.Setup(s => s.ExtractVariables(It.IsAny<string>()))
            .Returns((string content) => content.Contains("{NAME}") && content.Contains("{DATE}")
                ? new List<string> { "NAME", "DATE" }
                : new List<string>());

        var viewModel = CreateViewModel();

        // Act
        viewModel.TemplateContent = "Hello {NAME}, today is {DATE}";

        // Assert
        await Assert.That(viewModel.ExtractedVariables).IsNotNull();
        await Assert.That(viewModel.ExtractedVariables.Count).IsGreaterThan(0);
    }
}
