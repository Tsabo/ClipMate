namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Database registration tests for DatabaseContextFactory.
/// </summary>
public partial class DatabaseContextFactoryTests
{
    [Test]
    [Category("RegisterDatabase")]
    public async Task RegisterDatabase_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        await Assert.That(() => factory.RegisterDatabase(null!))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("RegisterDatabase")]
    public async Task RegisterDatabase_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        await Assert.That(() => factory.RegisterDatabase(string.Empty))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("RegisterDatabase")]
    public async Task RegisterDatabase_WithValidPath_AddsToLoadedDatabases()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        factory.RegisterDatabase(_testDbPath);
        var loadedPaths = factory.GetLoadedDatabasePaths();

        // Assert
        await Assert.That(loadedPaths.Count).IsEqualTo(1);
        await Assert.That(loadedPaths.First()).Contains(_testDbPath);
    }

    [Test]
    [Category("RegisterDatabase")]
    public async Task RegisterDatabase_WithSamePathTwice_OnlyRegistersOnce()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        factory.RegisterDatabase(_testDbPath);
        factory.RegisterDatabase(_testDbPath);
        var loadedPaths = factory.GetLoadedDatabasePaths();

        // Assert
        await Assert.That(loadedPaths.Count).IsEqualTo(1);
    }

    [Test]
    [Category("RegisterDatabase")]
    public async Task RegisterDatabase_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var factory = CreateFactory();
        factory.Dispose();

        // Act & Assert
        await Assert.That(() => factory.RegisterDatabase(_testDbPath))
            .Throws<ObjectDisposedException>();
    }
}
