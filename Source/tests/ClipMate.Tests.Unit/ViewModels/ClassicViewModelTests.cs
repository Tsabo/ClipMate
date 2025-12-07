using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for ClassicViewModel.
/// Tests the simplified Classic window with command stubs and basic clip loading.
/// </summary>
public class ClassicViewModelTests
{
    private readonly MainMenuViewModel _mainMenuViewModel;
    private readonly Mock<IClipService> _mockClipService;
    private readonly Mock<IMessenger> _mockMessenger;
    private readonly ClassicViewModel _viewModel;

    public ClassicViewModelTests()
    {
        _mockClipService = new Mock<IClipService>();
        _mockMessenger = new Mock<IMessenger>();
        _mainMenuViewModel = new MainMenuViewModel(_mockMessenger.Object);
        _viewModel = new ClassicViewModel(_mockClipService.Object, _mainMenuViewModel);
    }

    [Test]
    public async Task Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var mainMenu = new MainMenuViewModel(_mockMessenger.Object);
        var viewModel = new ClassicViewModel(_mockClipService.Object, mainMenu);

        // Assert
        await Assert.That(viewModel).IsNotNull();
        await Assert.That(viewModel.IsDroppedDown).IsFalse();
        await Assert.That(viewModel.IsTacked).IsFalse();
        await Assert.That(viewModel.ShouldCloseWindow).IsFalse();
        await Assert.That(viewModel.Clips).IsNotNull();
        await Assert.That(viewModel.Collections).IsNotNull();
    }

    [Test]
    public async Task LoadRecentClipsAsync_ShouldLoadSpecifiedNumberOfClips()
    {
        // Arrange
        var clips = CreateSampleClips(20);
        _mockClipService.Setup(p => p.GetRecentAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        await _viewModel.LoadRecentClipsAsync(20);

        // Assert
        await Assert.That(_viewModel.Clips.Count).IsEqualTo(20);
        _mockClipService.Verify(p => p.GetRecentAsync(20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LoadRecentClipsAsync_WithDefaultCount_ShouldLoad50Clips()
    {
        // Arrange
        var clips = CreateSampleClips(50);
        _mockClipService.Setup(p => p.GetRecentAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        await _viewModel.LoadRecentClipsAsync();

        // Assert
        await Assert.That(_viewModel.Clips.Count).IsEqualTo(50);
    }

    [Test]
    public async Task LoadRecentClipsAsync_WithClips_ShouldSetSelectedClip()
    {
        // Arrange
        var clips = CreateSampleClips(3);
        _mockClipService.Setup(p => p.GetRecentAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        await _viewModel.LoadRecentClipsAsync();

        // Assert
        await Assert.That(_viewModel.SelectedClip).IsNotNull();
        await Assert.That(_viewModel.SelectedClip).IsEqualTo(clips[0]);
        await Assert.That(_viewModel.SelectedClipTitle).IsNotEmpty();
    }

    [Test]
    public async Task ToggleDropDownCommand_ShouldToggleIsDroppedDown()
    {
        // Arrange
        var initialState = _viewModel.IsDroppedDown;

        // Act
        _viewModel.ToggleDropDownCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.IsDroppedDown).IsEqualTo(!initialState);

        // Act again
        _viewModel.ToggleDropDownCommand.Execute(null);

        // Assert - back to initial
        await Assert.That(_viewModel.IsDroppedDown).IsEqualTo(initialState);
    }

    [Test]
    public async Task ToggleTackCommand_ShouldToggleIsTacked()
    {
        // Arrange
        var initialState = _viewModel.IsTacked;

        // Act
        _viewModel.ToggleTackCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.IsTacked).IsEqualTo(!initialState);

        // Act again
        _viewModel.ToggleTackCommand.Execute(null);

        // Assert - back to initial
        await Assert.That(_viewModel.IsTacked).IsEqualTo(initialState);
    }

    [Test]
    public async Task CloseWindowCommand_ShouldSetShouldCloseWindow()
    {
        // Arrange
        await Assert.That(_viewModel.ShouldCloseWindow).IsFalse();

        // Act
        _viewModel.CloseWindowCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ShouldCloseWindow).IsTrue();
    }

    [Test]
    public async Task Collections_ShouldContainDefaultCollections()
    {
        // Assert
        await Assert.That(_viewModel.Collections).Contains("Inbox");
        await Assert.That(_viewModel.Collections).Contains("Safe");
        await Assert.That(_viewModel.Collections).Contains("Overflow");
        await Assert.That(_viewModel.Collections).Contains("Samples");
        await Assert.That(_viewModel.Collections).Contains("Trash Can");
    }

    [Test]
    public void MainMenu_CommandStubs_ShouldNotThrow()
    {
        // Assert that all menu command stubs can be executed without throwing
        _viewModel.MainMenu.CreateNewClipCommand.Execute(null);
        _viewModel.MainMenu.ClipPropertiesCommand.Execute(null);
        _viewModel.MainMenu.RenameClipCommand.Execute(null);
        _viewModel.MainMenu.DeleteSelectedCommand.Execute(null);
        _viewModel.MainMenu.ExitCommand.Execute(null);
        _viewModel.MainMenu.UndoCommand.Execute(null);
        _viewModel.MainMenu.SelectAllCommand.Execute(null);
        _viewModel.MainMenu.OptionsCommand.Execute(null);
        _viewModel.MainMenu.SearchCommand.Execute(null);
        _viewModel.MainMenu.AboutCommand.Execute(null);
        _viewModel.MainMenu.PowerPasteToggleCommand.Execute(null);

        // If we get here without an exception, the test passes
    }

    private static List<Clip> CreateSampleClips(int count)
    {
        var clips = new List<Clip>();
        for (var i = 1; i <= count; i++)
        {
            clips.Add(new Clip
            {
                Id = Guid.NewGuid(),
                TextContent = $"Sample Clip {i}",
                Type = ClipType.Text,
                CapturedAt = DateTime.UtcNow.AddMinutes(-i),
                SourceApplicationName = "Test App",
                ContentHash = $"hash{i}",
            });
        }

        return clips;
    }
}
