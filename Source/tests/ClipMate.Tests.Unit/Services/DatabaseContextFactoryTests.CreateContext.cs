namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Context creation tests for DatabaseContextFactory.
/// </summary>
public partial class DatabaseContextFactoryTests
{
    [Test]
    [Category("CreateContext")]
    public async Task CreateContext_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        await Assert.That(() => factory.CreateContext(null!))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("CreateContext")]
    public async Task CreateContext_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        await Assert.That(() => factory.CreateContext(string.Empty))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("CreateContext")]
    public async Task CreateContext_WithWhitespacePath_ThrowsArgumentException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        await Assert.That(() => factory.CreateContext("   "))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("CreateContext")]
    public async Task CreateContext_WithValidPath_ReturnsContext()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        await using var context = factory.CreateContext(_testDbPath);

        // Assert
        await Assert.That(context).IsNotNull();
        await Assert.That(context.Database).IsNotNull();
    }

    [Test]
    [Category("CreateContext")]
    public async Task CreateContext_WithValidPath_RegistersDatabase()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        await using var context = factory.CreateContext(_testDbPath);
        var loadedPaths = factory.GetLoadedDatabasePaths();

        // Assert
        await Assert.That(loadedPaths).Count().IsEqualTo(1);
        await Assert.That(loadedPaths.First()).Contains(_testDbPath);
    }

    [Test]
    [Category("CreateContext")]
    public async Task CreateContext_WithRelativePath_NormalizesToAbsolutePath()
    {
        // Arrange
        var factory = CreateFactory();
        const string relativePath = "test.db";

        // Act
        await using var context = factory.CreateContext(relativePath);
        var loadedPaths = factory.GetLoadedDatabasePaths();

        // Assert
        await Assert.That(loadedPaths).Count().IsEqualTo(1);
        await Assert.That(loadedPaths.First()).IsNotEqualTo(relativePath);
        await Assert.That(Path.IsPathRooted(loadedPaths.First())).IsTrue();
    }

    [Test]
    [Category("CreateContext")]
    public async Task CreateContext_CalledMultipleTimes_ReturnsNewInstancesEachTime()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        await using var context1 = factory.CreateContext(_testDbPath);
        await using var context2 = factory.CreateContext(_testDbPath);

        // Assert - Different instances (not cached)
        await Assert.That(ReferenceEquals(context1, context2)).IsFalse();
    }

    [Test]
    [Category("CreateContext")]
    public async Task CreateContext_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var factory = CreateFactory();
        factory.Dispose();

        // Act & Assert
        await Assert.That(() => factory.CreateContext(_testDbPath))
            .Throws<ObjectDisposedException>();
    }
}
