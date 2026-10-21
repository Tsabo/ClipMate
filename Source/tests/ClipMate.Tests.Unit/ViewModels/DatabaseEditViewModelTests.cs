using ClipMate.App.ViewModels;

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
    public async Task DatabaseIdText_NewDatabase_ExplainsWhenItIsAssigned()
    {
        // Arrange & Act
        var viewModel = new DatabaseEditViewModel();

        // Assert
        await Assert.That(viewModel.DatabaseIdText).IsEqualTo("Assigned when the database is opened");
    }
}
