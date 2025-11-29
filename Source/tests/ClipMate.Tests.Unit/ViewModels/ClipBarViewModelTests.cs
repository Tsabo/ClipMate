using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

public class ClipBarViewModelTests
{
    private readonly Mock<IClipService> _mockClipService;
    private readonly Mock<IPasteService> _mockPasteService;
    private readonly ClipBarViewModel _viewModel;

    public ClipBarViewModelTests()
    {
        _mockClipService = new Mock<IClipService>();
        _mockPasteService = new Mock<IPasteService>();
        _viewModel = new ClipBarViewModel(_mockClipService.Object, _mockPasteService.Object);
    }

    [Test]
    public async Task LoadRecentClipsAsync_ShouldLoadSpecifiedNumberOfClips()
    {
        // Arrange
        var clips = CreateSampleClips(20);
        _mockClipService.Setup(x => x.GetRecentAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        await _viewModel.LoadRecentClipsAsync(20);

        // Assert
        await Assert.That(_viewModel.Clips.Count).IsEqualTo(20);
        _mockClipService.Verify(x => x.GetRecentAsync(20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LoadRecentClipsAsync_WithDefaultCount_ShouldLoad20Clips()
    {
        // Arrange
        var clips = CreateSampleClips(20);
        _mockClipService.Setup(x => x.GetRecentAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        await _viewModel.LoadRecentClipsAsync();

        // Assert
        await Assert.That(_viewModel.Clips.Count).IsEqualTo(20);
    }

    [Test]
    public async Task FilterCommand_WithSearchText_ShouldFilterClips()
    {
        // Arrange
        _viewModel.Clips.Clear();
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Hello World", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Goodbye", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Hello Again", Type = ClipType.Text });

        // Act
        _viewModel.SearchText = "Hello";

        // Assert
        await Assert.That(_viewModel.FilteredClips.Count).IsEqualTo(2);
        await Assert.That(_viewModel.FilteredClips.All(c => (c.TextContent ?? string.Empty).Contains("Hello", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task FilterCommand_WithEmptySearchText_ShouldShowAllClips()
    {
        // Arrange
        _viewModel.Clips.Clear();
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Hello", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "World", Type = ClipType.Text });
        _viewModel.SearchText = "test"; // Trigger filter first
        _viewModel.FilteredClips.Clear(); // Clear

        // Act
        _viewModel.SearchText = "";

        // Assert
        await Assert.That(_viewModel.FilteredClips.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SelectClipCommand_ShouldPasteClipContent()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), TextContent = "Test Content", Type = ClipType.Text };
        _mockPasteService.Setup(x => x.PasteToActiveWindowAsync(clip, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _viewModel.SelectClipCommand.ExecuteAsync(clip);

        // Assert
        _mockPasteService.Verify(x => x.PasteToActiveWindowAsync(clip, It.IsAny<CancellationToken>()), Times.Once);
        await Assert.That(_viewModel.ShouldCloseWindow).IsTrue();
    }

    [Test]
    public async Task SelectClipCommand_WithNullClip_ShouldNotPaste()
    {
        // Act
        await _viewModel.SelectClipCommand.ExecuteAsync(null);

        // Assert
        _mockPasteService.Verify(x => x.PasteToActiveWindowAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Never);
        await Assert.That(_viewModel.ShouldCloseWindow).IsFalse();
    }

    [Test]
    public async Task CancelCommand_ShouldSetCloseWindowFlag()
    {
        // Act
        _viewModel.CancelCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.ShouldCloseWindow).IsTrue();
    }

    [Test]
    public async Task NavigateUpCommand_ShouldSelectPreviousItem()
    {
        // Arrange
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "First", Type = ClipType.Text });
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Second", Type = ClipType.Text });
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Third", Type = ClipType.Text });
        _viewModel.SelectedIndex = 2;

        // Act
        _viewModel.NavigateUpCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task NavigateDownCommand_ShouldSelectNextItem()
    {
        // Arrange
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "First", Type = ClipType.Text });
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Second", Type = ClipType.Text });
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Third", Type = ClipType.Text });
        _viewModel.SelectedIndex = 0;

        // Act
        _viewModel.NavigateDownCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task NavigateUpCommand_AtFirstItem_ShouldWrapToLast()
    {
        // Arrange
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "First", Type = ClipType.Text });
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Second", Type = ClipType.Text });
        _viewModel.SelectedIndex = 0;

        // Act
        _viewModel.NavigateUpCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.SelectedIndex).IsEqualTo(1); // Wrap to last
    }

    [Test]
    public async Task NavigateDownCommand_AtLastItem_ShouldWrapToFirst()
    {
        // Arrange
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "First", Type = ClipType.Text });
        _viewModel.FilteredClips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Second", Type = ClipType.Text });
        _viewModel.SelectedIndex = 1;

        // Act
        _viewModel.NavigateDownCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.SelectedIndex).IsEqualTo(0); // Wrap to first
    }

    private static List<Clip> CreateSampleClips(int count)
    {
        var clips = new List<Clip>();
        for (int i = 1; i <= count; i++)
        {
            clips.Add(new Clip
            {
                Id = Guid.NewGuid(),
                TextContent = $"Sample Clip {i}",
                Type = ClipType.Text,
                CapturedAt = DateTime.UtcNow.AddMinutes(-i),
                SourceApplicationName = "Test App",
                ContentHash = $"hash{i}"
            });
        }
        return clips;
    }
}
