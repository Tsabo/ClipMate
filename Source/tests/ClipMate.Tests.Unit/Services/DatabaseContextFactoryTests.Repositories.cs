using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Repository factory method tests for DatabaseContextFactory.
/// </summary>
public partial class DatabaseContextFactoryTests
{
    [Test]
    [Category("Repositories")]
    public async Task GetClipRepository_WithDatabaseKey_ReturnsRepository()
    {
        // Arrange
        var databases = new Dictionary<string, DatabaseConfiguration>
        {
            { "test", new DatabaseConfiguration { Name = "Test", FilePath = _testDbPath } },
        };

        var configService = CreateConfigService(databases);
        var factory = CreateFactory(configService);

        // Act
        var repository = factory.GetClipRepository("test");

        // Assert
        await Assert.That(repository).IsNotNull();
    }

    [Test]
    [Category("Repositories")]
    public async Task GetClipRepository_WithFilePath_ReturnsRepository()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var repository = factory.GetClipRepository(_testDbPath);

        // Assert
        await Assert.That(repository).IsNotNull();
    }

    [Test]
    [Category("Repositories")]
    public async Task GetBlobRepository_WithDatabaseKey_ReturnsRepository()
    {
        // Arrange
        var databases = new Dictionary<string, DatabaseConfiguration>
        {
            { "test", new DatabaseConfiguration { Name = "Test", FilePath = _testDbPath } },
        };

        var configService = CreateConfigService(databases);
        var factory = CreateFactory(configService);

        // Act
        var repository = factory.GetBlobRepository("test");

        // Assert
        await Assert.That(repository).IsNotNull();
    }

    [Test]
    [Category("Repositories")]
    public async Task GetClipDataRepository_WithDatabaseKey_ReturnsRepository()
    {
        // Arrange
        var databases = new Dictionary<string, DatabaseConfiguration>
        {
            { "test", new DatabaseConfiguration { Name = "Test", FilePath = _testDbPath } },
        };

        var configService = CreateConfigService(databases);
        var factory = CreateFactory(configService);

        // Act
        var repository = factory.GetClipDataRepository("test");

        // Assert
        await Assert.That(repository).IsNotNull();
    }

    [Test]
    [Category("Repositories")]
    public async Task GetCollectionRepository_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var factory = CreateFactory();
        factory.Dispose();

        // Act & Assert
        await Assert.That(() => factory.GetCollectionRepository(_testDbPath))
            .Throws<ObjectDisposedException>();
    }
}
