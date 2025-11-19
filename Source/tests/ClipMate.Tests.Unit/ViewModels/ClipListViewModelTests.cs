using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

public class ClipListViewModelTests
{
    private readonly Mock<IClipService> _mockClipService;
    private readonly Mock<IMessenger> _mockMessenger;
    private readonly ClipListViewModel _viewModel;

    public ClipListViewModelTests()
    {
        _mockClipService = new Mock<IClipService>();
        _mockMessenger = new Mock<IMessenger>();
        _viewModel = new ClipListViewModel(_mockClipService.Object, _mockMessenger.Object);
    }

    [Fact]
    public void IsListView_DefaultValue_IsTrue()
    {
        var viewModel = new ClipListViewModel(_mockClipService.Object, _mockMessenger.Object);
        Assert.True(viewModel.IsListView);
    }

    [Fact]
    public void IsGridView_DefaultValue_IsFalse()
    {
        var viewModel = new ClipListViewModel(_mockClipService.Object, _mockMessenger.Object);
        Assert.False(viewModel.IsGridView);
    }

    [Fact]
    public void Constructor_ShouldRegisterWithMessenger()
    {
        // Verify that Register was called on the messenger
        _mockMessenger.Verify(m => m.Register<ClipListViewModel, ClipMate.Core.Events.ClipAddedEvent>(
            It.IsAny<ClipListViewModel>(),
            It.IsAny<MessageHandler<ClipListViewModel, ClipMate.Core.Events.ClipAddedEvent>>()), 
            Times.Once);
    }
}
