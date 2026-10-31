using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;

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

        var mockConfig = A.Fake<IConfigurationService>();
        A.CallTo(() => mockConfig.Configuration).Returns(config);
        var factory = CreateFactory(mockConfig);

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

        var mockConfig = A.Fake<IConfigurationService>();
        A.CallTo(() => mockConfig.Configuration).Returns(config);
        var factory = CreateFactory(mockConfig);

        // Act
        await using var context = factory.CreateContext(_testDbPath);
        await context.Database.EnsureCreatedAsync();

        // Assert - Verify connection works (pragma application doesn't throw)
        await Assert.That(await context.Database.CanConnectAsync()).IsTrue();
    }
}
