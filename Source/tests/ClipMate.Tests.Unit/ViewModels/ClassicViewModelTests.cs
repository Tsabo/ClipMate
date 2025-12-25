using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for ClassicViewModel.
/// Tests the simplified Classic window with command stubs and basic clip loading.
/// </summary>
public class ClassicViewModelTests
{
    private readonly Mock<IMessenger> _mockMessenger;
    private readonly ClassicViewModel _viewModel;

    public ClassicViewModelTests()
    {
        _mockMessenger = new Mock<IMessenger>();
        var mainMenuViewModel = new MainMenuViewModel(
            _mockMessenger.Object,
            new Mock<IUndoService>().Object);

        // Create QuickPasteToolbarViewModel with mocked dependencies
        var mockQuickPasteService = new Mock<IQuickPasteService>();
        var mockConfigService = new Mock<IConfigurationService>();
        // Setup Configuration property to return a valid ClipMateConfiguration
        mockConfigService.Setup(p => p.Configuration).Returns(new ClipMateConfiguration());
        var quickPasteToolbar = new QuickPasteToolbarViewModel(
            mockQuickPasteService.Object,
            mockConfigService.Object,
            _mockMessenger.Object,
            NullLogger<QuickPasteToolbarViewModel>.Instance);

        // Create ClipListViewModel with mocked dependencies
        var mockCollectionService = new Mock<ICollectionService>();
        var mockFolderService = new Mock<IFolderService>();
        var mockClipService = new Mock<IClipService>();
        var mockQuickPasteServiceForClipList = new Mock<IQuickPasteService>();
        var mockRepositoryFactory = new Mock<IClipRepositoryFactory>();
        var clipListViewModel = new ClipListViewModel(
            mockCollectionService.Object,
            mockFolderService.Object,
            mockClipService.Object,
            mockQuickPasteServiceForClipList.Object,
            mockRepositoryFactory.Object,
            _mockMessenger.Object,
            NullLogger<ClipListViewModel>.Instance);

        _viewModel = new ClassicViewModel(mainMenuViewModel, quickPasteToolbar, clipListViewModel);
    }

    [Test]
    public async Task Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var mainMenu = new MainMenuViewModel(
            _mockMessenger.Object,
            new Mock<IUndoService>().Object);

        var mockQuickPasteService = new Mock<IQuickPasteService>();
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(p => p.Configuration).Returns(new ClipMateConfiguration());
        var quickPasteToolbar = new QuickPasteToolbarViewModel(
            mockQuickPasteService.Object,
            mockConfigService.Object,
            _mockMessenger.Object,
            NullLogger<QuickPasteToolbarViewModel>.Instance);

        var mockCollectionService = new Mock<ICollectionService>();
        var mockFolderService = new Mock<IFolderService>();
        var mockClipService = new Mock<IClipService>();
        var mockQuickPasteServiceForClipList = new Mock<IQuickPasteService>();
        var mockRepositoryFactory = new Mock<IClipRepositoryFactory>();
        var clipListViewModel = new ClipListViewModel(
            mockCollectionService.Object,
            mockFolderService.Object,
            mockClipService.Object,
            mockQuickPasteServiceForClipList.Object,
            mockRepositoryFactory.Object,
            _mockMessenger.Object,
            NullLogger<ClipListViewModel>.Instance);

        var viewModel = new ClassicViewModel(mainMenu, quickPasteToolbar, clipListViewModel);

        // Assert
        await Assert.That(viewModel).IsNotNull();
        await Assert.That(viewModel.IsDroppedDown).IsFalse();
        await Assert.That(viewModel.IsTacked).IsFalse();
        await Assert.That(viewModel.ShouldCloseWindow).IsFalse();
        await Assert.That(viewModel.Collections).IsNotNull();
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
}
