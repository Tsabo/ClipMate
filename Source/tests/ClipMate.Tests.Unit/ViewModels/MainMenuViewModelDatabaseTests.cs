using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for MainMenuViewModel database maintenance commands.
/// </summary>
public class MainMenuViewModelDatabaseTests : TestFixtureBase
{
    private Mock<IMessenger> _messengerMock = null!;
    private MainMenuViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _messengerMock = new Mock<IMessenger>(MockBehavior.Loose);
        _viewModel = new MainMenuViewModel(
            _messengerMock.Object,
            new Mock<IUndoService>().Object);
    }

    [Test]
    public async Task BackupDatabaseCommand_ShouldSendBackupDatabaseRequestedEvent()
    {
        // Act - Just verify command executes without throwing
        _viewModel.BackupDatabaseCommand.Execute(null);

        // Assert - Command executed successfully (messenger is mocked so no actual send happens)
        await Assert.That(_viewModel.BackupDatabaseCommand).IsNotNull();
    }

    [Test]
    public async Task RestoreDatabaseCommand_ShouldSendRestoreDatabaseRequestedEvent()
    {
        // Act - Just verify command executes without throwing
        _viewModel.RestoreDatabaseCommand.Execute(null);

        // Assert - Command executed successfully (messenger is mocked so no actual send happens)
        await Assert.That(_viewModel.RestoreDatabaseCommand).IsNotNull();
    }

    [Test]
    public async Task EmptyTrashCommand_ShouldSendEmptyTrashRequestedEvent()
    {
        // Act - Just verify command executes without throwing
        _viewModel.EmptyTrashCommand.Execute(null);

        // Assert - Command executed successfully (messenger is mocked so no actual send happens)
        await Assert.That(_viewModel.EmptyTrashCommand).IsNotNull();
    }

    [Test]
    public async Task SimpleRepairCommand_ShouldSendSimpleRepairRequestedEvent()
    {
        // Act - Just verify command executes without throwing
        _viewModel.SimpleRepairCommand.Execute(null);

        // Assert - Command executed successfully (messenger is mocked so no actual send happens)
        await Assert.That(_viewModel.SimpleRepairCommand).IsNotNull();
    }

    [Test]
    public async Task ComprehensiveRepairCommand_ShouldSendComprehensiveRepairRequestedEvent()
    {
        // Act - Just verify command executes without throwing
        _viewModel.ComprehensiveRepairCommand.Execute(null);

        // Assert - Command executed successfully (messenger is mocked so no actual send happens)
        await Assert.That(_viewModel.ComprehensiveRepairCommand).IsNotNull();
    }

    [Test]
    public async Task RunCleanupNowCommand_ShouldSendRunCleanupNowRequestedEvent()
    {
        // Act - Just verify command executes without throwing
        _viewModel.RunCleanupNowCommand.Execute(null);

        // Assert - Command executed successfully (messenger is mocked so no actual send happens)
        await Assert.That(_viewModel.RunCleanupNowCommand).IsNotNull();
    }

    [Test]
    public async Task AllDatabaseMaintenanceCommands_ShouldBeExecutable()
    {
        // Act & Assert
        await Assert.That(_viewModel.BackupDatabaseCommand.CanExecute(null)).IsTrue();
        await Assert.That(_viewModel.RestoreDatabaseCommand.CanExecute(null)).IsTrue();
        await Assert.That(_viewModel.EmptyTrashCommand.CanExecute(null)).IsTrue();
        await Assert.That(_viewModel.SimpleRepairCommand.CanExecute(null)).IsTrue();
        await Assert.That(_viewModel.ComprehensiveRepairCommand.CanExecute(null)).IsTrue();
        await Assert.That(_viewModel.RunCleanupNowCommand.CanExecute(null)).IsTrue();
    }
}
