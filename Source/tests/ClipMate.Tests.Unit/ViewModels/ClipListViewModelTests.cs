using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Shouldly;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

public class ClipListViewModelTests
{
    private readonly Mock<IClipService> _mockClipService;
    private readonly ClipListViewModel _viewModel;

    public ClipListViewModelTests()
    {
        _mockClipService = new Mock<IClipService>();
        _viewModel = new ClipListViewModel(_mockClipService.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var viewModel = new ClipListViewModel(_mockClipService.Object);

        // Assert
        viewModel.Clips.ShouldBeEmpty();
        viewModel.SelectedClip.ShouldBeNull();
        viewModel.IsListView.ShouldBeTrue();
        viewModel.IsGridView.ShouldBeFalse();
        viewModel.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadClipsAsync_ShouldLoadClipsFromService()
    {
        // Arrange
        var clips = new List<Clip>
        {
            new() { Id = Guid.NewGuid(), TextContent = "Test 1", Type = ClipType.Text, CapturedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), TextContent = "Test 2", Type = ClipType.Text, CapturedAt = DateTime.UtcNow }
        };
        _mockClipService.Setup(s => s.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(clips);

        // Act
        await _viewModel.LoadClipsAsync();

        // Assert
        _viewModel.Clips.Count.ShouldBe(2);
        _viewModel.Clips.ShouldBe(clips);
        _mockClipService.Verify(s => s.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadClipsAsync_ShouldSetLoadingState()
    {
        // Arrange
        var clips = new List<Clip>
        {
            new() { Id = Guid.NewGuid(), TextContent = "Test", Type = ClipType.Text, CapturedAt = DateTime.UtcNow }
        };
        _mockClipService.Setup(s => s.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(clips);
        var loadingStates = new List<bool>();
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ClipListViewModel.IsLoading))
                loadingStates.Add(_viewModel.IsLoading);
        };

        // Act
        await _viewModel.LoadClipsAsync();

        // Assert
        loadingStates.ShouldBe(new[] { true, false });
    }

    [Fact]
    public void SelectedClip_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), TextContent = "Test", Type = ClipType.Text, CapturedAt = DateTime.UtcNow };
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ClipListViewModel.SelectedClip))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.SelectedClip = clip;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        _viewModel.SelectedClip.ShouldBe(clip);
    }

    [Fact]
    public void IsListView_WhenSetToTrue_ShouldSetIsGridViewToFalse()
    {
        // Arrange
        _viewModel.IsGridView = true;

        // Act
        _viewModel.IsListView = true;

        // Assert
        _viewModel.IsListView.ShouldBeTrue();
        _viewModel.IsGridView.ShouldBeFalse();
    }

    [Fact]
    public void IsGridView_WhenSetToTrue_ShouldSetIsListViewToFalse()
    {
        // Arrange
        _viewModel.IsListView = true;

        // Act
        _viewModel.IsGridView = true;

        // Assert
        _viewModel.IsGridView.ShouldBeTrue();
        _viewModel.IsListView.ShouldBeFalse();
    }

    [Fact]
    public void SwitchToListView_ShouldSetIsListViewToTrue()
    {
        // Arrange
        _viewModel.IsGridView = true;

        // Act
        _viewModel.SwitchToListView();

        // Assert
        _viewModel.IsListView.ShouldBeTrue();
        _viewModel.IsGridView.ShouldBeFalse();
    }

    [Fact]
    public void SwitchToGridView_ShouldSetIsGridViewToTrue()
    {
        // Arrange
        _viewModel.IsListView = true;

        // Act
        _viewModel.SwitchToGridView();

        // Assert
        _viewModel.IsGridView.ShouldBeTrue();
        _viewModel.IsListView.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadClipsAsync_WhenServiceThrows_ShouldHandleException()
    {
        // Arrange
        _mockClipService.Setup(s => s.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act & Assert
        await Should.NotThrowAsync(async () => await _viewModel.LoadClipsAsync());
        _viewModel.Clips.ShouldBeEmpty();
        _viewModel.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReloadClips()
    {
        // Arrange
        var initialClips = new List<Clip>
        {
            new() { Id = Guid.NewGuid(), TextContent = "Test 1", Type = ClipType.Text, CapturedAt = DateTime.UtcNow }
        };
        var refreshedClips = new List<Clip>
        {
            new() { Id = Guid.NewGuid(), TextContent = "Test 1", Type = ClipType.Text, CapturedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), TextContent = "Test 2", Type = ClipType.Text, CapturedAt = DateTime.UtcNow }
        };
        
        _mockClipService.SetupSequence(s => s.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialClips)
            .ReturnsAsync(refreshedClips);

        await _viewModel.LoadClipsAsync();

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Clips.Count.ShouldBe(2);
        _mockClipService.Verify(s => s.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
