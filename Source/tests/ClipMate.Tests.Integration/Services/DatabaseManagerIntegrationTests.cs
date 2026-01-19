using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

#pragma warning disable CA2000 // Dispose objects before losing scope - Test objects are disposed by test framework

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for DatabaseManager with real database contexts.
/// Tests database loading, unloading, and multi-database management.
/// </summary>
public class DatabaseManagerIntegrationTests : IntegrationTestBase
{
    private string _testDbPath1 = null!;
    private string _testDbPath2 = null!;

    [Before(Test)]
    public async Task SetupTestAsync()
    {
        await SetupAsync();
        
        // Create unique test database paths
        var guid = Guid.NewGuid().ToString("N")[..8];
        _testDbPath1 = Path.Combine(Path.GetTempPath(), $"clipmate_test_{guid}_1.db");
        _testDbPath2 = Path.Combine(Path.GetTempPath(), $"clipmate_test_{guid}_2.db");
    }

    [After(Test)]
    public async Task CleanupTestAsync()
    {
        await CleanupAsync();
        
        // Clean up test database files
        try
        {
            if (File.Exists(_testDbPath1)) File.Delete(_testDbPath1);
            if (File.Exists(_testDbPath2)) File.Delete(_testDbPath2);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Test]
    public async Task LoadAutoLoadDatabasesAsync_WithAutoLoadDatabases_LoadsThem()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "test1", new DatabaseConfiguration { Name = "Test DB 1", FilePath = _testDbPath1, AutoLoad = true } },
                { "test2", new DatabaseConfiguration { Name = "Test DB 2", FilePath = _testDbPath2, AutoLoad = true } },
                { "test3", new DatabaseConfiguration { Name = "Test DB 3", FilePath = "not_autoload.db", AutoLoad = false } },
            },
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var contextFactory = new TestDatabaseContextFactory();
        var logger = new Mock<ILogger<DatabaseManager>>();
        var messenger = new Mock<IMessenger>();
        var manager = new DatabaseManager(configService.Object, contextFactory, logger.Object, messenger.Object);

        // Act
        var count = await manager.LoadAutoLoadDatabasesAsync();

        // Assert
        await Assert.That(count).IsEqualTo(2);
        var loadedPaths = contextFactory.GetLoadedDatabasePaths().ToList();
        await Assert.That(loadedPaths).Contains(_testDbPath1);
        await Assert.That(loadedPaths).Contains(_testDbPath2);
        await Assert.That(loadedPaths).DoesNotContain("not_autoload.db");
    }

    [Test]
    public async Task LoadDatabaseAsync_WithValidDatabase_ReturnsTrue()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "testdb", new DatabaseConfiguration { Name = "Test Database", FilePath = _testDbPath1, AutoLoad = false } },
            },
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var contextFactory = new TestDatabaseContextFactory();
        var logger = new Mock<ILogger<DatabaseManager>>();
        var messenger = new Mock<IMessenger>();
        var manager = new DatabaseManager(configService.Object, contextFactory, logger.Object, messenger.Object);

        // Act
        var result = await manager.LoadDatabaseAsync("testdb");

        // Assert
        await Assert.That(result).IsTrue();
        var loadedPaths = contextFactory.GetLoadedDatabasePaths().ToList();
        await Assert.That(loadedPaths).Contains(_testDbPath1);
    }

    [Test]
    public async Task LoadDatabaseAsync_WithNonExistentDatabase_ReturnsFalse()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>(),
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var contextFactory = new TestDatabaseContextFactory();
        var logger = new Mock<ILogger<DatabaseManager>>();
        var messenger = new Mock<IMessenger>();
        var manager = new DatabaseManager(configService.Object, contextFactory, logger.Object, messenger.Object);

        // Act
        var result = await manager.LoadDatabaseAsync("NonExistent");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task CreateAllDatabaseContexts_WithLoadedDatabases_ReturnsDatabaseKeys()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "primary", new DatabaseConfiguration { Name = "My Clips", FilePath = _testDbPath1, AutoLoad = true } },
                { "secondary", new DatabaseConfiguration { Name = "Secondary", FilePath = _testDbPath2, AutoLoad = true } },
            },
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var contextFactory = new TestDatabaseContextFactory();
        var logger = new Mock<ILogger<DatabaseManager>>();
        var messenger = new Mock<IMessenger>();
        var manager = new DatabaseManager(configService.Object, contextFactory, logger.Object, messenger.Object);

        // Load the databases first
        await manager.LoadAutoLoadDatabasesAsync();

        // Act
        var contexts = manager.CreateAllDatabaseContexts().ToList();

        // Assert
        await Assert.That(contexts).Count().IsEqualTo(2);
        
        var primaryContext = contexts.FirstOrDefault(c => c.DatabaseKey == "primary");
        var secondaryContext = contexts.FirstOrDefault(c => c.DatabaseKey == "secondary");
        
        await Assert.That(primaryContext.DatabaseKey).IsNotNull();
        await Assert.That(secondaryContext.DatabaseKey).IsNotNull();
        await Assert.That(primaryContext.Context).IsNotNull();
        await Assert.That(secondaryContext.Context).IsNotNull();
    }

    [Test]
    public async Task CreateAllDatabaseContexts_ReturnsDatabaseKeysNotDisplayNames()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "key1", new DatabaseConfiguration { Name = "Display Name 1", FilePath = _testDbPath1, AutoLoad = true } },
                { "key2", new DatabaseConfiguration { Name = "Display Name 2", FilePath = _testDbPath2, AutoLoad = true } },
            },
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var contextFactory = new TestDatabaseContextFactory();
        var logger = new Mock<ILogger<DatabaseManager>>();
        var messenger = new Mock<IMessenger>();
        var manager = new DatabaseManager(configService.Object, contextFactory, logger.Object, messenger.Object);

        await manager.LoadAutoLoadDatabasesAsync();

        // Act
        var contexts = manager.CreateAllDatabaseContexts().ToList();

        // Assert - Should return config keys (key1, key2), not display names
        await Assert.That(contexts.Select(c => c.DatabaseKey)).Contains("key1");
        await Assert.That(contexts.Select(c => c.DatabaseKey)).Contains("key2");
        await Assert.That(contexts.Select(c => c.DatabaseKey)).DoesNotContain("Display Name 1");
        await Assert.That(contexts.Select(c => c.DatabaseKey)).DoesNotContain("Display Name 2");
    }

    [Test]
    public async Task UnloadDatabase_WithLoadedDatabase_UnloadsSuccessfully()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "testdb", new DatabaseConfiguration { Name = "Test DB", FilePath = _testDbPath1, AutoLoad = true } },
            },
        };

        configService.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var contextFactory = new TestDatabaseContextFactory();
        var logger = new Mock<ILogger<DatabaseManager>>();
        var messenger = new Mock<IMessenger>();
        var manager = new DatabaseManager(configService.Object, contextFactory, logger.Object, messenger.Object);

        await manager.LoadAutoLoadDatabasesAsync();
        
        // Verify loaded
        var loadedPaths = contextFactory.GetLoadedDatabasePaths().ToList();
        await Assert.That(loadedPaths).Contains(_testDbPath1);

        // Act
        var result = manager.UnloadDatabase("testdb");

        // Assert
        await Assert.That(result).IsTrue();
        loadedPaths = contextFactory.GetLoadedDatabasePaths().ToList();
        await Assert.That(loadedPaths).DoesNotContain(_testDbPath1);
    }

    /// <summary>
    /// Test implementation of IDatabaseContextFactory that creates real database contexts.
    /// </summary>
    private class TestDatabaseContextFactory : IDatabaseContextFactory
    {
        private readonly Dictionary<string, ClipMateDbContext> _contexts = new();
        private readonly HashSet<string> _registeredPaths = new();

        public ClipMateDbContext CreateContext(string databaseFilePath)
        {
            // Register the path
            _registeredPaths.Add(databaseFilePath);

            var options = new DbContextOptionsBuilder<ClipMateDbContext>()
                .UseSqlite($"Data Source={databaseFilePath}")
                .Options;

            var context = new ClipMateDbContext(options);
            
            // Ensure database is created
            context.Database.EnsureCreated();
            
            return context;
        }

        public void RegisterDatabase(string databasePath)
        {
            _registeredPaths.Add(databasePath);
        }

        public bool CloseDatabase(string databasePath)
        {
            return _registeredPaths.Remove(databasePath);
        }

        public IReadOnlyCollection<string> GetLoadedDatabasePaths()
        {
            return _registeredPaths.ToList().AsReadOnly();
        }

        public bool IsLoaded(string databaseFilePath)
        {
            return _registeredPaths.Contains(databaseFilePath);
        }

        public void Unload(string databaseFilePath)
        {
            _registeredPaths.Remove(databaseFilePath);
        }

        // Repository methods - not needed for DatabaseManager tests but required by interface
        public IClipRepository GetClipRepository(string databaseKey) => throw new NotImplementedException();
        public IClipDataRepository GetClipDataRepository(string databaseKey) => throw new NotImplementedException();
        public IBlobRepository GetBlobRepository(string databaseKey) => throw new NotImplementedException();
        public IShortcutRepository GetShortcutRepository(string databaseKey) => throw new NotImplementedException();
        public IUserRepository GetUserRepository(string databaseKey) => throw new NotImplementedException();
        public IMonacoEditorStateRepository GetMonacoEditorStateRepository(string databaseKey) => throw new NotImplementedException();
        public ICollectionRepository GetCollectionRepository(string databaseKey) => throw new NotImplementedException();
        public IFolderRepository GetFolderRepository(string databaseKey) => throw new NotImplementedException();
        public IApplicationFilterRepository GetApplicationFilterRepository(string databaseKey) => throw new NotImplementedException();
        public ITemplateRepository GetTemplateRepository(string databaseKey) => throw new NotImplementedException();
        public ISearchQueryRepository GetSearchQueryRepository(string databaseKey) => throw new NotImplementedException();

        public void Dispose()
        {
            foreach (var context in _contexts.Values)
            {
                context.Dispose();
            }
            _contexts.Clear();
            _registeredPaths.Clear();
        }
    }
}
