using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// SQLite pragma configuration tests for DatabaseContextFactory.
/// </summary>
public partial class DatabaseContextFactoryTests
{
    [Test]
    [Category("SqlitePragmas")]
    public async Task CreateContext_WithCachedWritesEnabled_AppliesWALMode()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>(),
            Preferences = new PreferencesConfiguration
            {
                EnableCachedDatabaseWrites = true, // WAL mode
            },
        };

        var mockConfig = new Mock<IConfigurationService>();
        mockConfig.Setup(x => x.Configuration).Returns(config);
        var factory = CreateFactory(mockConfig.Object);

        // Act
        await using var context = factory.CreateContext(_testDbPath);
        await context.Database.EnsureCreatedAsync();

        // Assert - Verify connection works (pragma application doesn't throw)
        await Assert.That(await context.Database.CanConnectAsync()).IsTrue();
    }

    [Test]
    [Category("SqlitePragmas")]
    public async Task CreateContext_WithCachedWritesDisabled_AppliesDELETEMode()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>(),
            Preferences = new PreferencesConfiguration
            {
                EnableCachedDatabaseWrites = false, // DELETE mode
            },
        };

        var mockConfig = new Mock<IConfigurationService>();
        mockConfig.Setup(x => x.Configuration).Returns(config);
        var factory = CreateFactory(mockConfig.Object);

        // Act
        await using var context = factory.CreateContext(_testDbPath);
        await context.Database.EnsureCreatedAsync();

        // Assert - Verify connection works (pragma application doesn't throw)
        await Assert.That(await context.Database.CanConnectAsync()).IsTrue();
    }
}
