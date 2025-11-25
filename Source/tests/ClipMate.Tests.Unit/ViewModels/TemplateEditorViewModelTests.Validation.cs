using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for TemplateEditorViewModel validation.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task IsFormValid_WithValidNameAndContent_ShouldBeTrue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TemplateName = "Valid Template";
        viewModel.TemplateContent = "Valid Content";

        // Act
        var isValid = viewModel.IsFormValid;

        // Assert
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task IsFormValid_WithEmptyName_ShouldBeFalse()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TemplateName = "";
        viewModel.TemplateContent = "Valid Content";

        // Act
        var isValid = viewModel.IsFormValid;

        // Assert
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task IsFormValid_WithEmptyContent_ShouldBeFalse()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TemplateName = "Valid Name";
        viewModel.TemplateContent = "";

        // Act
        var isValid = viewModel.IsFormValid;

        // Assert
        await Assert.That(isValid).IsFalse();
    }
}
