using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base test class for DatabaseContextFactory tests.
/// Contains shared setup, cleanup, and helper methods.
/// </summary>
[Category("DatabaseContextFactory")]
[Category("Unit")]
public partial class DatabaseContextFactoryTests
{
    private string _testDbPath = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
    }

    [After(Test)]
    public void Cleanup()
    {
        if (!File.Exists(_testDbPath))
            return;

        try
        {
            File.Delete(_testDbPath);
        }
        catch
        {
            /* Ignore cleanup errors */
        }
    }

    private DatabaseContextFactory CreateFactory(IConfigurationService? configService = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        configService ??= CreateConfigService();
        var logger = new Mock<ILogger<DatabaseContextFactory>>().Object;

        return new DatabaseContextFactory(serviceProvider, configService, logger);
    }

    private IConfigurationService CreateConfigService(Dictionary<string, DatabaseConfiguration>? databases = null)
    {
        var config = new ClipMateConfiguration
        {
            Databases = databases ?? new Dictionary<string, DatabaseConfiguration>(),
            Preferences = new PreferencesConfiguration
            {
                EnableCachedDatabaseWrites = false,
            },
        };

        var mockConfig = new Mock<IConfigurationService>();
        mockConfig.Setup(p => p.Configuration).Returns(config);
        return mockConfig.Object;
    }
}
