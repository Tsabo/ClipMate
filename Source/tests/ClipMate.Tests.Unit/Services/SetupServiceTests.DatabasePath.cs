namespace ClipMate.Tests.Unit.Services;

public partial class SetupServiceTests
{
    [Test]
    [Category("GetDefaultDatabasePath")]
    public async Task GetDefaultDatabasePath_ReturnsValidPath()
    {
        // Arrange
        var service = CreateService();

        // Act
        var path = service.GetDefaultDatabasePath();

        // Assert
        await Assert.That(path).IsNotNull();
        await Assert.That(path).IsNotEmpty();
        await Assert.That(path).Contains("ClipMate");
        await Assert.That(path).Contains("Databases");
        await Assert.That(path).EndsWith(".db");
    }

    [Test]
    [Category("GetDefaultDatabasePath")]
    public async Task GetDefaultDatabasePath_ReturnsConsistentPath()
    {
        // Arrange
        var service = CreateService();

        // Act
        var path1 = service.GetDefaultDatabasePath();
        var path2 = service.GetDefaultDatabasePath();

        // Assert
        await Assert.That(path1).IsEqualTo(path2);
    }

    [Test]
    [Category("GetDefaultDatabasePath")]
    public async Task GetDefaultDatabasePath_UsesLocalApplicationData()
    {
        // Arrange
        var service = CreateService();
        var expectedBasePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Act
        var path = service.GetDefaultDatabasePath();

        // Assert
        await Assert.That(path).StartsWith(expectedBasePath);
    }
}
