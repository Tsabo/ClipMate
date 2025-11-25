using ClipMate.App.ViewModels;
using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
    #region Property Change Tests

    [Test]
    public async Task InputText_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TextToolsViewModel.InputText))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.InputText = "Test input";

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(_viewModel.InputText).IsEqualTo("Test input");
    }

    [Test]
    public async Task SelectedTool_WhenChanged_ShouldUpdateProperty()
    {
        // Arrange
        var initialTool = _viewModel.SelectedTool;

        // Act
        _viewModel.SelectedTool = TextTool.SortLines;

        // Assert
        await Assert.That(_viewModel.SelectedTool).IsEqualTo(TextTool.SortLines);
        await Assert.That(_viewModel.SelectedTool).IsNotEqualTo(initialTool);
    }

    #endregion
}
