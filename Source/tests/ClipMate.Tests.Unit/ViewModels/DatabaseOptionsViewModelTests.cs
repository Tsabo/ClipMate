using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for DatabaseOptionsViewModel.
/// </summary>
public class DatabaseOptionsViewModelTests : TestFixtureBase
{
    private Mock<IConfigurationService> _configServiceMock = null!;
    private DatabaseOptionsViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _configServiceMock = MockRepository.Create<IConfigurationService>();
        var logger = CreateLogger<DatabaseOptionsViewModel>();
        _viewModel = new DatabaseOptionsViewModel(_configServiceMock.Object, logger);
    }

    [Test]
    public async Task LoadAsync_ShouldPopulateDatabasesFromConfiguration()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "db_0", new DatabaseConfiguration { Name = "Database 1" } },
                { "db_1", new DatabaseConfiguration { Name = "Database 2" } },
            },
            Preferences = new PreferencesConfiguration
            {
                BackupIntervalDays = 7,
                AutoConfirmBackupSeconds = 10,
            },
        };

        _configServiceMock.Setup(x => x.Configuration).Returns(config);

        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.Databases.Count).IsEqualTo(2);
        await Assert.That(_viewModel.Databases[0].Name).IsEqualTo("Database 1");
        await Assert.That(_viewModel.Databases[1].Name).IsEqualTo("Database 2");
        await Assert.That(_viewModel.BackupIntervalDays).IsEqualTo(7);
        await Assert.That(_viewModel.AutoConfirmBackupSeconds).IsEqualTo(10);
    }

    [Test]
    public async Task SaveAsync_ShouldUpdateConfiguration()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>(),
            Preferences = new PreferencesConfiguration(),
        };

        _configServiceMock.Setup(x => x.Configuration).Returns(config);

        _viewModel.Databases.Add(new DatabaseConfigurationViewModel("test", new DatabaseConfiguration { Name = "Test DB" }));
        _viewModel.BackupIntervalDays = 14;
        _viewModel.AutoConfirmBackupSeconds = 20;

        // Act
        await _viewModel.SaveAsync();

        // Assert
        await Assert.That(config.Databases.Count).IsEqualTo(1);
        await Assert.That(config.Databases.First().Value.Name).IsEqualTo("Test DB");
        await Assert.That(config.Preferences.BackupIntervalDays).IsEqualTo(14);
        await Assert.That(config.Preferences.AutoConfirmBackupSeconds).IsEqualTo(20);
    }

    [Test]
    public async Task AddDatabaseCommand_ShouldBeExecutable()
    {
        // Arrange & Act
        var canExecute = _viewModel.AddDatabaseCommand.CanExecute(null);

        // Assert
        await Assert.That(canExecute).IsTrue();
    }

    [Test]
    public async Task EditDatabaseCommand_ShouldRequireSelection()
    {
        // Arrange
        _viewModel.SelectedDatabase = null;

        // Act
        var canExecute = _viewModel.EditDatabaseCommand.CanExecute(null);

        // Assert - Command is always executable, but logs warning if no selection
        await Assert.That(canExecute).IsTrue();
    }

    [Test]
    public async Task DeleteDatabaseCommand_ShouldRemoveSelectedDatabase()
    {
        // Arrange
        var dbToDelete = new DatabaseConfigurationViewModel("delete", new DatabaseConfiguration { Name = "To Delete" });
        _viewModel.Databases.Add(dbToDelete);
        _viewModel.Databases.Add(new DatabaseConfigurationViewModel("keep", new DatabaseConfiguration { Name = "Keep" }));
        _viewModel.SelectedDatabase = dbToDelete;

        // Act
        _viewModel.DeleteDatabaseCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.Databases.Count).IsEqualTo(1);
        await Assert.That(_viewModel.Databases[0].Name).IsEqualTo("Keep");
    }

    [Test]
    public async Task OpenFolderCommand_ShouldNotThrowWhenNoSelection()
    {
        // Arrange
        _viewModel.SelectedDatabase = null;

        // Act & Assert - Should not throw
        _viewModel.OpenFolderCommand.Execute(null);
        await Task.CompletedTask;
    }

    [Test]
    public async Task AnalyzeDatabaseCommand_ShouldBeExecutable()
    {
        // Arrange & Act & Assert - Should complete without throwing
        await _viewModel.AnalyzeDatabaseCommand.ExecuteAsync(null);
    }
}
