namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Database closure tests for DatabaseContextFactory.
/// </summary>
public partial class DatabaseContextFactoryTests
{
    [Test]
    [Category("CloseDatabase")]
    public async Task CloseDatabase_WithRegisteredDatabase_ReturnsTrue()
    {
        // Arrange
        var factory = CreateFactory();
        factory.RegisterDatabase(_testDbPath);

        // Act
        var result = factory.CloseDatabase(_testDbPath);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(factory.GetLoadedDatabasePaths()).IsEmpty();
    }

    [Test]
    [Category("CloseDatabase")]
    public async Task CloseDatabase_WithNonRegisteredDatabase_ReturnsFalse()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var result = factory.CloseDatabase(_testDbPath);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("CloseDatabase")]
    public async Task CloseDatabase_WithRelativePath_NormalizesAndMatches()
    {
        // Arrange
        var factory = CreateFactory();
        const string relativePath = "test.db";
        factory.RegisterDatabase(relativePath);

        // Act - Close using relative path again
        var result = factory.CloseDatabase(relativePath);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(factory.GetLoadedDatabasePaths()).IsEmpty();
    }

    [Test]
    [Category("CloseDatabase")]
    public async Task CloseDatabase_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var factory = CreateFactory();
        factory.Dispose();

        // Act & Assert
        await Assert.That(() => factory.CloseDatabase(_testDbPath))
            .Throws<ObjectDisposedException>();
    }
}
