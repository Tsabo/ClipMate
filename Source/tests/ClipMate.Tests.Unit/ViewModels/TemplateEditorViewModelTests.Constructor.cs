using ClipMate.App.ViewModels;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Constructor validation tests for TemplateEditorViewModel.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task Constructor_WithNullTemplateService_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new TemplateEditorViewModel(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidService_ShouldInitializeProperties()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        await Assert.That(viewModel.TemplateName).IsEmpty();
        await Assert.That(viewModel.TemplateContent).IsEmpty();
        await Assert.That(viewModel.TemplateDescription).IsEmpty();
        await Assert.That(viewModel.Templates).IsNotNull();
        await Assert.That(viewModel.Templates.Count).IsEqualTo(0);
    }
}
