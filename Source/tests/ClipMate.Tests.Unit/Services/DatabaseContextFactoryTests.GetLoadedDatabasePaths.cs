namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Database path query tests for DatabaseContextFactory.
/// </summary>
public partial class DatabaseContextFactoryTests
{
    [Test]
    [Category("GetLoadedDatabasePaths")]
    public async Task GetLoadedDatabasePaths_WhenEmpty_ReturnsEmptyCollection()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var paths = factory.GetLoadedDatabasePaths();

        // Assert
        await Assert.That(paths).IsEmpty();
    }

    [Test]
    [Category("GetLoadedDatabasePaths")]
    public async Task GetLoadedDatabasePaths_WithMultipleDatabases_ReturnsAllPaths()
    {
        // Arrange
        var factory = CreateFactory();
        var path1 = Path.Combine(Path.GetTempPath(), "db1.db");
        var path2 = Path.Combine(Path.GetTempPath(), "db2.db");

        // Act
        factory.RegisterDatabase(path1);
        factory.RegisterDatabase(path2);
        var paths = factory.GetLoadedDatabasePaths();

        // Assert
        await Assert.That(paths.Count).IsEqualTo(2);
    }

    [Test]
    [Category("GetLoadedDatabasePaths")]
    public async Task GetLoadedDatabasePaths_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var factory = CreateFactory();
        factory.Dispose();

        // Act & Assert
        await Assert.That(() => factory.GetLoadedDatabasePaths())
            .Throws<ObjectDisposedException>();
    }
}
