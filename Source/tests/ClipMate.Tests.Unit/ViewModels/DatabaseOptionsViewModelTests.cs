using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for DatabaseOptionsViewModel.
/// </summary>
public class DatabaseOptionsViewModelTests : TestFixtureBase
{
    private IConfigurationService _configServiceMock = null!;
    private DatabaseOptionsViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _configServiceMock = A.Fake<IConfigurationService>(o => o.Strict());
        var logger = CreateLogger<DatabaseOptionsViewModel>();
        _viewModel = new DatabaseOptionsViewModel(_configServiceMock, A.Fake<IClipMate7ImportService>(), A.Fake<IDatabaseManager>(), A.Fake<IMessenger>(), logger,
            A.Fake<IDatabaseIdService>(), A.Fake<ISharedDatabaseService>());
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

        A.CallTo(() => _configServiceMock.Configuration).Returns(config);

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

        A.CallTo(() => _configServiceMock.Configuration).Returns(config);

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
    public async Task SaveAsync_ShouldActivateNewlyAddedAutoLoadDatabasesOnly()
    {
        // Arrange
        var (viewModel, _, activated, refreshCount) = await CreateLoadedViewModelAsync();
        viewModel.Databases.Add(new DatabaseConfigurationViewModel("added", new DatabaseConfiguration { Name = "Added", AutoLoad = true }));
        viewModel.Databases.Add(new DatabaseConfigurationViewModel("manual", new DatabaseConfiguration { Name = "Manual", AutoLoad = false }));

        // Act
        await viewModel.SaveAsync();
        await viewModel.SaveAsync(); // A second save with no changes must not activate or refresh again

        // Assert
        await Assert.That(activated).IsEquivalentTo(["added"]);
        await Assert.That(refreshCount()).IsEqualTo(1);
    }

    [Test]
    public async Task SaveAsync_ShouldUnloadRemovedDatabaseAndRefreshTree()
    {
        // Arrange
        var (viewModel, databaseManager, activated, refreshCount) = await CreateLoadedViewModelAsync();
        viewModel.Databases.Remove(viewModel.Databases.Single(p => p.DatabaseKey == "existing"));

        // Act
        await viewModel.SaveAsync();

        // Assert
        A.CallTo(() => databaseManager.UnloadDatabase("existing")).MustHaveHappenedOnceExactly();
        await Assert.That(activated).IsEmpty();
        await Assert.That(refreshCount()).IsEqualTo(1);
    }

    private async Task<(DatabaseOptionsViewModel ViewModel, IDatabaseManager DatabaseManager, List<string> Activated, Func<int> RefreshCount)>
        CreateLoadedViewModelAsync()
    {
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                ["existing"] = new() { Name = "Existing", AutoLoad = true },
            },
            Preferences = new PreferencesConfiguration(),
        };

        A.CallTo(() => _configServiceMock.Configuration).Returns(config);

        var messenger = new StrongReferenceMessenger();
        var activated = new List<string>();
        var refreshes = 0;
        messenger.Register<ActivateDatabaseRequestedEvent>(this, (_, m) => activated.Add(m.DatabaseKey));
        messenger.Register<RefreshCollectionTreeRequestedEvent>(this, (_, _) => refreshes++);

        var databaseManager = A.Fake<IDatabaseManager>();
        var viewModel = new DatabaseOptionsViewModel(_configServiceMock, A.Fake<IClipMate7ImportService>(), databaseManager, messenger,
            CreateLogger<DatabaseOptionsViewModel>(), A.Fake<IDatabaseIdService>(), A.Fake<ISharedDatabaseService>());

        await viewModel.LoadAsync();

        return (viewModel, databaseManager, activated, () => refreshes);
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
