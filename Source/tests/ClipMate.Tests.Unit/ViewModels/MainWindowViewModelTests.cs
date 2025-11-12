using ClipMate.App.ViewModels;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for MainWindowViewModel.
/// Following TDD: Tests written FIRST, then implementation.
/// </summary>
public class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel();

        // Assert
        viewModel.ShouldNotBeNull();
        viewModel.Title.ShouldBe("ClipMate");
        viewModel.WindowWidth.ShouldBeGreaterThan(0);
        viewModel.WindowHeight.ShouldBeGreaterThan(0);
        viewModel.IsBusy.ShouldBeFalse();
        viewModel.StatusMessage.ShouldBeEmpty();
    }

    [Fact]
    public void WindowWidth_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.WindowWidth))
                propertyChangedRaised = true;
        };

        // Act - set to a DIFFERENT value
        viewModel.WindowWidth = 1500;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        viewModel.WindowWidth.ShouldBe(1500);
    }

    [Fact]
    public void WindowHeight_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.WindowHeight))
                propertyChangedRaised = true;
        };

        // Act - set to a DIFFERENT value
        viewModel.WindowHeight = 900;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        viewModel.WindowHeight.ShouldBe(900);
    }

    [Fact]
    public void IsBusy_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsBusy))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.IsBusy = true;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        viewModel.IsBusy.ShouldBeTrue();
    }

    [Fact]
    public void StatusMessage_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.StatusMessage))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.StatusMessage = "Loading clips...";

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        viewModel.StatusMessage.ShouldBe("Loading clips...");
    }

    [Fact]
    public void SetStatus_ShouldUpdateStatusMessage()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act
        viewModel.SetStatus("Ready");

        // Assert
        viewModel.StatusMessage.ShouldBe("Ready");
    }

    [Fact]
    public void SetBusy_WithTrue_ShouldSetIsBusyAndShowMessage()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act
        viewModel.SetBusy(true, "Processing...");

        // Assert
        viewModel.IsBusy.ShouldBeTrue();
        viewModel.StatusMessage.ShouldBe("Processing...");
    }

    [Fact]
    public void SetBusy_WithFalse_ShouldClearIsBusyAndMessage()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        viewModel.SetBusy(true, "Processing...");

        // Act
        viewModel.SetBusy(false);

        // Assert
        viewModel.IsBusy.ShouldBeFalse();
        viewModel.StatusMessage.ShouldBeEmpty();
    }

    [Fact]
    public void LeftPaneWidth_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.LeftPaneWidth))
                propertyChangedRaised = true;
        };

        // Act - set to a DIFFERENT value
        viewModel.LeftPaneWidth = 300;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        viewModel.LeftPaneWidth.ShouldBe(300);
    }

    [Fact]
    public void RightPaneWidth_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.RightPaneWidth))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.RightPaneWidth = 350;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        viewModel.RightPaneWidth.ShouldBe(350);
    }
}
