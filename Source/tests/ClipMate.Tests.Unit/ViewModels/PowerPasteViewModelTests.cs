using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Moq;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Unit.ViewModels;

public class PowerPasteViewModelTests
{
    private readonly Mock<IClipService> _mockClipService;
    private readonly Mock<IPasteService> _mockPasteService;
    private readonly PowerPasteViewModel _viewModel;

    public PowerPasteViewModelTests()
    {
        _mockClipService = new Mock<IClipService>();
        _mockPasteService = new Mock<IPasteService>();
        _viewModel = new PowerPasteViewModel(_mockClipService.Object, _mockPasteService.Object);
    }

    [Fact]
    public async Task LoadRecentClipsAsync_ShouldLoadSpecifiedNumberOfClips()
    {
        // Arrange
        var clips = CreateSampleClips(20);
        _mockClipService.Setup(x => x.GetRecentAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        await _viewModel.LoadRecentClipsAsync(20);

        // Assert
        _viewModel.Clips.Count.ShouldBe(20);
        _mockClipService.Verify(x => x.GetRecentAsync(20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadRecentClipsAsync_WithDefaultCount_ShouldLoad20Clips()
    {
        // Arrange
        var clips = CreateSampleClips(20);
        _mockClipService.Setup(x => x.GetRecentAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);

        // Act
        await _viewModel.LoadRecentClipsAsync();

        // Assert
        _viewModel.Clips.Count.ShouldBe(20);
    }

    [Fact]
    public void FilterCommand_WithSearchText_ShouldFilterClips()
    {
        // Arrange
        _viewModel.Clips.Clear();
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Hello World", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Goodbye", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Hello Again", Type = ClipType.Text });

        // Act
        _viewModel.SearchText = "Hello";

        // Assert
        _viewModel.FilteredClips.Count.ShouldBe(2);
        _viewModel.FilteredClips.ShouldAllBe(c => (c.TextContent ?? string.Empty).Contains("Hello", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FilterCommand_WithEmptySearchText_ShouldShowAllClips()
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
        _viewModel.FilteredClips.Count.ShouldBe(2);
    }

    [Fact]
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
        _viewModel.ShouldCloseWindow.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectClipCommand_WithNullClip_ShouldNotPaste()
    {
        // Act
        await _viewModel.SelectClipCommand.ExecuteAsync(null);

        // Assert
        _mockPasteService.Verify(x => x.PasteToActiveWindowAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Never);
        _viewModel.ShouldCloseWindow.ShouldBeFalse();
    }

    [Fact]
    public void CancelCommand_ShouldSetCloseWindowFlag()
    {
        // Act
        _viewModel.CancelCommand.Execute(null);

        // Assert
        _viewModel.ShouldCloseWindow.ShouldBeTrue();
    }

    [Fact]
    public void NavigateUpCommand_ShouldSelectPreviousItem()
    {
        // Arrange
        _viewModel.Clips.Clear();
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "First", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Second", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Third", Type = ClipType.Text });
        _viewModel.SearchText = ""; // Trigger filtering
        _viewModel.SelectedIndex = 2;

        // Act
        _viewModel.NavigateUpCommand.Execute(null);

        // Assert
        _viewModel.SelectedIndex.ShouldBe(1);
    }

    [Fact]
    public void NavigateDownCommand_ShouldSelectNextItem()
    {
        // Arrange
        _viewModel.Clips.Clear();
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "First", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Second", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Third", Type = ClipType.Text });
        _viewModel.SearchText = ""; // Trigger filtering
        _viewModel.SelectedIndex = 0;

        // Act
        _viewModel.NavigateDownCommand.Execute(null);

        // Assert
        _viewModel.SelectedIndex.ShouldBe(1);
    }

    [Fact]
    public void NavigateUpCommand_AtFirstItem_ShouldWrapToLast()
    {
        // Arrange
        _viewModel.Clips.Clear();
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "First", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Second", Type = ClipType.Text });
        _viewModel.SearchText = ""; // Trigger filtering
        _viewModel.SelectedIndex = 0;

        // Act
        _viewModel.NavigateUpCommand.Execute(null);

        // Assert
        _viewModel.SelectedIndex.ShouldBe(1); // Wrap to last
    }

    [Fact]
    public void NavigateDownCommand_AtLastItem_ShouldWrapToFirst()
    {
        // Arrange
        _viewModel.Clips.Clear();
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "First", Type = ClipType.Text });
        _viewModel.Clips.Add(new Clip { Id = Guid.NewGuid(), TextContent = "Second", Type = ClipType.Text });
        _viewModel.SearchText = ""; // Trigger filtering
        _viewModel.SelectedIndex = 1;

        // Act
        _viewModel.NavigateDownCommand.Execute(null);

        // Assert
        _viewModel.SelectedIndex.ShouldBe(0); // Wrap to first
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
