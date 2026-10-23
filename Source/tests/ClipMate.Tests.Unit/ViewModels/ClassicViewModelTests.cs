using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for ClassicViewModel.
/// Tests the simplified Classic window with command stubs and basic clip loading.
/// </summary>
public class ClassicViewModelTests
{
    private readonly IMessenger _mockMessenger;
    private readonly ClassicViewModel _viewModel;

    public ClassicViewModelTests()
    {
        _mockMessenger = A.Fake<IMessenger>();

        // Create mock service provider that can resolve IDatabaseManager, AboutDialogViewModel, and IActiveWindowService
        var mockDatabaseManager = A.Fake<IDatabaseManager>();
        A.CallTo(() => mockDatabaseManager.GetLoadedDatabases()).Returns([]);
        var mockUpdateCheckService = A.Fake<IUpdateCheckService>();
        var mockAboutLogger = A.Fake<ILogger<AboutDialogViewModel>>();
        var aboutDialogViewModel = new AboutDialogViewModel(mockUpdateCheckService, mockAboutLogger);
        var mockActiveWindowService = A.Fake<IActiveWindowService>();
        var mockServiceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => mockServiceProvider.GetService(typeof(IDatabaseManager))).Returns(mockDatabaseManager);
        A.CallTo(() => mockServiceProvider.GetService(typeof(AboutDialogViewModel))).Returns(aboutDialogViewModel);
        A.CallTo(() => mockServiceProvider.GetService(typeof(IActiveWindowService))).Returns(mockActiveWindowService);

        var mainMenuViewModel = new MainMenuViewModel(
            _mockMessenger,
            A.Fake<IUndoService>(),
            A.Fake<IClipViewerWindowManager>(),
            A.Fake<IClipboardService>(),
            A.Fake<IAutoAppendService>(),
            A.Fake<IPowerPasteService>(),
            A.Fake<IQuickPasteService>(),
            A.Fake<IPasteTarget>(),
            A.Fake<IPasteTargetTracker>(),
            mockServiceProvider);

        // Create QuickPasteToolbarViewModel with mocked dependencies
        var mockQuickPasteService = A.Fake<IQuickPasteService>();
        var mockConfigService = A.Fake<IConfigurationService>();
        // Setup Configuration property to return a valid ClipMateConfiguration
        A.CallTo(() => mockConfigService.Configuration).Returns(new ClipMateConfiguration());
        var quickPasteToolbar = new QuickPasteToolbarViewModel(
            mockQuickPasteService,
            A.Fake<IPasteTarget>(),
            A.Fake<IPasteTargetTracker>(),
            mockConfigService,
            _mockMessenger,
            NullLogger<QuickPasteToolbarViewModel>.Instance);

        // Create ClipListViewModel with mocked dependencies
        var mockCollectionService = A.Fake<ICollectionService>();
        var mockFolderService = A.Fake<IFolderService>();
        var mockClipService = A.Fake<IClipService>();
        var mockQuickPasteServiceForClipList = A.Fake<IQuickPasteService>();
        var mockContextFactory = A.Fake<IDatabaseContextFactory>();
        var clipListViewModel = new ClipListViewModel(
            mockCollectionService,
            mockFolderService,
            mockClipService,
            mockQuickPasteServiceForClipList,
            mockContextFactory,
            A.Fake<IRemoteDatabaseService>(),
            _mockMessenger,
            NullLogger<ClipListViewModel>.Instance);

        _viewModel = new ClassicViewModel(
            mainMenuViewModel,
            quickPasteToolbar,
            clipListViewModel);
    }

    [Test]
    public async Task Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        // Create mock service provider that can resolve IDatabaseManager, AboutDialogViewModel, and IActiveWindowService
        var mockDatabaseManager = A.Fake<IDatabaseManager>();
        A.CallTo(() => mockDatabaseManager.GetLoadedDatabases()).Returns([]);
        var mockUpdateCheckService = A.Fake<IUpdateCheckService>();
        var mockAboutLogger = A.Fake<ILogger<AboutDialogViewModel>>();
        var aboutDialogViewModel = new AboutDialogViewModel(mockUpdateCheckService, mockAboutLogger);
        var mockActiveWindowService = A.Fake<IActiveWindowService>();
        var mockServiceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => mockServiceProvider.GetService(typeof(IDatabaseManager))).Returns(mockDatabaseManager);
        A.CallTo(() => mockServiceProvider.GetService(typeof(AboutDialogViewModel))).Returns(aboutDialogViewModel);
        A.CallTo(() => mockServiceProvider.GetService(typeof(IActiveWindowService))).Returns(mockActiveWindowService);

        var mainMenu = new MainMenuViewModel(
            _mockMessenger,
            A.Fake<IUndoService>(),
            A.Fake<IClipViewerWindowManager>(),
            A.Fake<IClipboardService>(),
            A.Fake<IAutoAppendService>(),
            A.Fake<IPowerPasteService>(),
            A.Fake<IQuickPasteService>(),
            A.Fake<IPasteTarget>(),
            A.Fake<IPasteTargetTracker>(),
            mockServiceProvider);

        var mockQuickPasteService = A.Fake<IQuickPasteService>();
        var mockConfigService = A.Fake<IConfigurationService>();
        A.CallTo(() => mockConfigService.Configuration).Returns(new ClipMateConfiguration());
        var quickPasteToolbar = new QuickPasteToolbarViewModel(
            mockQuickPasteService,
            A.Fake<IPasteTarget>(),
            A.Fake<IPasteTargetTracker>(),
            mockConfigService,
            _mockMessenger,
            NullLogger<QuickPasteToolbarViewModel>.Instance);

        var mockCollectionService = A.Fake<ICollectionService>();
        var mockFolderService = A.Fake<IFolderService>();
        var mockClipService = A.Fake<IClipService>();
        var mockQuickPasteServiceForClipList = A.Fake<IQuickPasteService>();
        var mockContextFactory = A.Fake<IDatabaseContextFactory>();
        var clipListViewModel = new ClipListViewModel(
            mockCollectionService,
            mockFolderService,
            mockClipService,
            mockQuickPasteServiceForClipList,
            mockContextFactory,
            A.Fake<IRemoteDatabaseService>(),
            _mockMessenger,
            NullLogger<ClipListViewModel>.Instance);

        var viewModel = new ClassicViewModel(
            mainMenu,
            quickPasteToolbar,
            clipListViewModel);

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
        // Note: AboutCommand is excluded as it's a real implementation that creates UI dialogs
        _viewModel.MainMenu.CreateNewClipCommand.Execute(null);
        _viewModel.MainMenu.ClipPropertiesCommand.Execute(null);
        _viewModel.MainMenu.RenameClipCommand.Execute(null);
        _viewModel.MainMenu.DeleteSelectedCommand.Execute(null);
        _viewModel.MainMenu.ExitCommand.Execute(null);
        _viewModel.MainMenu.UndoCommand.Execute(null);
        _viewModel.MainMenu.SelectAllCommand.Execute(null);
        _viewModel.MainMenu.OptionsCommand.Execute(null);
        _viewModel.MainMenu.SearchCommand.Execute(null);
        _viewModel.MainMenu.PowerPasteToggleCommand.Execute(null);

        // If we get here without an exception, the test passes
    }
}
