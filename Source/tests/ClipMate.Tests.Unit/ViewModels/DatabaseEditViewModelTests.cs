using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

public class DatabaseEditViewModelTests
{
    [Test]
    public async Task DatabaseIdText_WithDatabaseId_ShowsDatabaseId()
    {
        // Arrange
        var viewModel = new DatabaseEditViewModel();

        // Act
        viewModel.SetDatabaseId(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"));

        // Assert
        await Assert.That(viewModel.DatabaseIdText).IsEqualTo("0f8fad5b-d9cb-469f-a165-70867728950e");
    }

    [Test]
    public async Task DatabaseIdText_WithoutDatabaseId_ExplainsWhenItIsAssigned()
    {
        // Arrange
        var viewModel = new DatabaseEditViewModel();

        // Act
        viewModel.SetDatabaseId(null);

        // Assert
        await Assert.That(viewModel.DatabaseIdText).IsEqualTo("Assigned when the database is opened");
    }

    [Test]
    public async Task ShareWithPeers_RoundTripsThroughTheConfiguration()
    {
        // Arrange
        var viewModel = new DatabaseEditViewModel();

        // Act
        viewModel.LoadFrom(new DatabaseConfiguration { Name = "Work", FilePath = @"C:\clips\work.db", ShareWithPeers = true });

        // Assert
        await Assert.That(viewModel.ShareWithPeers).IsTrue();
        await Assert.That(viewModel.ToConfiguration().ShareWithPeers).IsTrue();
    }

    [Test]
    public async Task CheckSharingAsync_AnotherSharedDatabaseHasTheSameDatabaseId_RefusesToShareAndOffersANewId()
    {
        // Arrange
        var (viewModel, sharedDatabases, _) = CreateWithSharing();
        var original = new DatabaseConfiguration { Name = "Work", FilePath = @"C:\clips\work.db", ShareWithPeers = true };
        A.CallTo(() => sharedDatabases.FindDatabaseIdConflictAsync(@"C:\clips\copy.db", A<IEnumerable<DatabaseConfiguration>>._, A<CancellationToken>._))
            .Returns(original);

        // Act
        viewModel.ShareWithPeers = true;
        await viewModel.CheckSharingAsync();

        // Assert
        await Assert.That(viewModel.HasShareConflict).IsTrue();
        await Assert.That(viewModel.ShareConflictMessage).Contains("Work");
        await Assert.That(viewModel.CanAccept).IsFalse();
        await Assert.That(viewModel.GiveNewDatabaseIdCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task GiveNewDatabaseIdAsync_AfterAConflict_AssignsANewIdAndAllowsSharing()
    {
        // Arrange
        var (viewModel, sharedDatabases, databaseIds) = CreateWithSharing();
        var original = new DatabaseConfiguration { Name = "Work", FilePath = @"C:\clips\work.db", ShareWithPeers = true };
        var newId = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
        A.CallTo(() => sharedDatabases.FindDatabaseIdConflictAsync(A<string>._, A<IEnumerable<DatabaseConfiguration>>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(original, null);
        A.CallTo(() => databaseIds.AssignNewDatabaseIdAsync(@"C:\clips\copy.db", A<CancellationToken>._)).Returns(newId);
        viewModel.ShareWithPeers = true; // checks at once
        await Assert.That(viewModel.HasShareConflict).IsTrue();

        // Act
        await viewModel.GiveNewDatabaseIdCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.DatabaseIdText).IsEqualTo("0f8fad5b-d9cb-469f-a165-70867728950e");
        await Assert.That(viewModel.HasShareConflict).IsFalse();
        await Assert.That(viewModel.CanAccept).IsTrue();
    }

    [Test]
    public async Task CheckSharingAsync_NotShared_IsNeverAConflict()
    {
        // Arrange
        var (viewModel, sharedDatabases, _) = CreateWithSharing();

        // Act
        viewModel.ShareWithPeers = false;
        await viewModel.CheckSharingAsync();

        // Assert
        await Assert.That(viewModel.HasShareConflict).IsFalse();
        A.CallTo(() => sharedDatabases.FindDatabaseIdConflictAsync(A<string>._, A<IEnumerable<DatabaseConfiguration>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private static (DatabaseEditViewModel ViewModel, ISharedDatabaseService SharedDatabases, IDatabaseIdService DatabaseIds) CreateWithSharing()
    {
        var sharedDatabases = A.Fake<ISharedDatabaseService>();
        var databaseIds = A.Fake<IDatabaseIdService>();
        var viewModel = new DatabaseEditViewModel();
        viewModel.LoadFrom(new DatabaseConfiguration { Name = "Work copy", FilePath = @"C:\clips\copy.db" });
        viewModel.UseSharingChecks(new DatabaseSharingContext(sharedDatabases, databaseIds,
            [new DatabaseConfiguration { Name = "Work", FilePath = @"C:\clips\work.db", ShareWithPeers = true }]));
        return (viewModel, sharedDatabases, databaseIds);
    }

    [Test]
    public async Task DatabaseIdText_NewDatabase_ExplainsWhenItIsAssigned()
    {
        // Arrange & Act
        var viewModel = new DatabaseEditViewModel();

        // Assert
        await Assert.That(viewModel.DatabaseIdText).IsEqualTo("Assigned when the database is opened");
    }
}
