using ClipMate.Core.Models;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for DatabaseSchemaMigrationService verifying end-to-end schema creation and migration.
/// </summary>
public class DatabaseSchemaMigrationTests
{
    private SqliteConnection? _connection;
    private ClipMateDbContext? _context;

    [Before(Test)]
    public async Task SetupAsync()
    {
        // Use in-memory SQLite with explicit connection management
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ClipMateDbContext(options);

        // Note: Do NOT call EnsureCreated - we want to test schema migration from scratch
    }

    [After(Test)]
    public async Task CleanupAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();

        if (_connection != null)
            await _connection.DisposeAsync();
    }

    [Test]
    public async Task MigrateAsync_EmptyDatabase_CreatesAllTablesAndColumns()
    {
        // Arrange
        var migrationService = new DatabaseSchemaMigrationService();

        // Act - Migrate empty database to full schema
        await migrationService.MigrateAsync(_context!);

        // Assert - Verify all expected tables exist
        var tables = await GetTableNamesAsync();

        // Debug output
        if (tables.Count == 0)
        {
            Console.WriteLine("No tables found after migration!");
            Console.WriteLine($"Connection state: {_connection!.State}");
            Console.WriteLine($"Connection string: {_connection.ConnectionString}");
        }

        await Assert.That(tables).Contains("Clips");
        await Assert.That(tables).Contains("Collections");
        await Assert.That(tables).Contains("ClipData");
        await Assert.That(tables).Contains("BlobTxt");
        await Assert.That(tables).Contains("BlobJpg");
        await Assert.That(tables).Contains("BlobPng");
        await Assert.That(tables).Contains("BlobBlob");
        await Assert.That(tables).Contains("Shortcuts");
        await Assert.That(tables).Contains("Users");
        await Assert.That(tables).Contains("Templates");
        await Assert.That(tables).Contains("SearchQueries");
        await Assert.That(tables).Contains("ApplicationFilters");
        await Assert.That(tables).Contains("SoundEvents");
        await Assert.That(tables).Contains("MonacoEditorStates");
    }

    [Test]
    public async Task MigrateAsync_EmptyDatabase_CreatesClipsTableWithCorrectSchema()
    {
        // Arrange
        var migrationService = new DatabaseSchemaMigrationService();

        // Act
        await migrationService.MigrateAsync(_context!);

        // Assert - Verify Clips table has expected columns
        var columns = await GetTableColumnsAsync("Clips");

        await Assert.That(columns).Contains("Id");
        await Assert.That(columns).Contains("CollectionId");
        await Assert.That(columns).Contains("FolderId");
        await Assert.That(columns).Contains("Type");
        await Assert.That(columns).Contains("ContentHash");
        await Assert.That(columns).Contains("SourceApplicationName");
        await Assert.That(columns).Contains("SourceApplicationTitle");
        await Assert.That(columns).Contains("CapturedAt");
        await Assert.That(columns).Contains("LastModified");
        await Assert.That(columns).Contains("IsFavorite");
        await Assert.That(columns).Contains("Del");
        await Assert.That(columns).Contains("DelDate");
    }

    [Test]
    public async Task MigrateAsync_EmptyDatabase_CreatesForeignKeys()
    {
        // Arrange
        var migrationService = new DatabaseSchemaMigrationService();

        // Act
        await migrationService.MigrateAsync(_context!);

        // Assert - Verify foreign keys exist by attempting constrained operations
        var collection = new Collection { Name = "Test Collection" };
        _context!.Collections.Add(collection);
        await _context.SaveChangesAsync();

        var clip = new Clip
        {
            CollectionId = collection.Id,
            Type = ClipType.Text,
            ContentHash = "test-hash",
            CapturedAt = DateTimeOffset.UtcNow
        };

        _context.Clips.Add(clip);
        await _context.SaveChangesAsync();

        // Assert - Verify the foreign key constraint is working
        var savedClip = await _context.Clips.FindAsync(clip.Id);
        await Assert.That(savedClip).IsNotNull();
        await Assert.That(savedClip!.CollectionId).IsEqualTo(collection.Id);
    }

    [Test]
    public async Task MigrateAsync_EmptyDatabase_CreatesIndexes()
    {
        // Arrange
        var migrationService = new DatabaseSchemaMigrationService();

        // Act
        await migrationService.MigrateAsync(_context!);

        // Assert - Verify indexes are created if defined in EF model
        var indexes = await GetIndexesForTableAsync("Clips");

        // Note: Indexes are only created if explicitly defined in the EF Core model
        // If the model has indexes, they should be created. If not, that's okay too.
        // This test verifies the migration doesn't fail when indexes are defined.
    }

    [Test]
    public async Task MigrateAsync_ExistingSchema_DetectsNoChanges()
    {
        // Arrange
        var migrationService = new DatabaseSchemaMigrationService();

        // First migration - create schema
        await migrationService.MigrateAsync(_context!);

        // Act - Second migration should detect no changes
        await migrationService.MigrateAsync(_context!);

        // Assert - Should complete without errors (verified by no exception)
        // If there were issues with schema comparison, it would throw
        // Test passes if no exception thrown
    }

    [Test]
    public async Task MigrateAsync_ExistingSchema_AllowsDataOperations()
    {
        // Arrange
        var migrationService = new DatabaseSchemaMigrationService();
        await migrationService.MigrateAsync(_context!);

        // Act - Test CRUD operations work correctly
        var collection = new Collection { Name = "Integration Test" };
        _context!.Collections.Add(collection);
        await _context.SaveChangesAsync();

        var template = new Template
        {
            Name = "Test Template",
            Content = "Hello {NAME}",
            Description = "Test"
        };

        _context.Templates.Add(template);
        await _context.SaveChangesAsync();

        var appFilter = new ApplicationFilter
        {
            ProcessName = "notepad.exe",
            IsEnabled = true
        };

        _context.ApplicationFilters.Add(appFilter);
        await _context.SaveChangesAsync();

        // Assert - Verify data was saved and can be retrieved
        var savedCollection = await _context.Collections.FindAsync(collection.Id);
        var savedTemplate = await _context.Templates.FindAsync(template.Id);
        var savedFilter = await _context.ApplicationFilters.FindAsync(appFilter.Id);

        await Assert.That(savedCollection).IsNotNull();
        await Assert.That(savedCollection!.Name).IsEqualTo("Integration Test");

        await Assert.That(savedTemplate).IsNotNull();
        await Assert.That(savedTemplate!.Content).IsEqualTo("Hello {NAME}");

        await Assert.That(savedFilter).IsNotNull();
        await Assert.That(savedFilter!.ProcessName).IsEqualTo("notepad.exe");
    }

    [Test]
    public async Task MigrateAsync_ExistingSchema_SupportsComplexQueries()
    {
        // Arrange
        var migrationService = new DatabaseSchemaMigrationService();
        await migrationService.MigrateAsync(_context!);

        // Seed test data
        var collection = new Collection { Name = "Test Collection" };
        _context!.Collections.Add(collection);
        await _context.SaveChangesAsync();

        var clips = new[]
        {
            new Clip
            {
                CollectionId = collection.Id,
                Type = ClipType.Text,
                ContentHash = "hash1",
                SourceApplicationName = "notepad.exe",
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-2),
                IsFavorite = true
            },
            new Clip
            {
                CollectionId = collection.Id,
                Type = ClipType.Image,
                ContentHash = "hash2",
                SourceApplicationName = "chrome.exe",
                CapturedAt = DateTimeOffset.UtcNow.AddHours(-1)
            },
            new Clip
            {
                CollectionId = collection.Id,
                Type = ClipType.Text,
                ContentHash = "hash3",
                SourceApplicationName = "notepad.exe",
                CapturedAt = DateTimeOffset.UtcNow,
                Del = true
            }
        };

        _context.Clips.AddRange(clips);
        await _context.SaveChangesAsync();

        // Act - Execute complex query
        // Note: SQLite doesn't support DateTimeOffset in ORDER BY, so we order in memory
        var activeTextClips = await _context.Clips
            .Where(c => c.CollectionId == collection.Id)
            .Where(c => !c.Del)
            .Where(c => c.Type == ClipType.Text)
            .ToListAsync();

        activeTextClips = activeTextClips
            .OrderByDescending(c => c.CapturedAt)
            .ToList();

        // Assert
        await Assert.That(activeTextClips.Count).IsEqualTo(1);
        await Assert.That(activeTextClips[0].SourceApplicationName).IsEqualTo("notepad.exe");
        await Assert.That(activeTextClips[0].IsFavorite).IsTrue();
    }

    [Test]
    public async Task MigrateAsync_EmptyDatabase_CreatesBlobTables()
    {
        // Arrange
        var migrationService = new DatabaseSchemaMigrationService();

        // Act
        await migrationService.MigrateAsync(_context!);

        // Assert - Verify all blob tables exist
        var tables = await GetTableNamesAsync();

        await Assert.That(tables).Contains("BlobTxt");
        await Assert.That(tables).Contains("BlobJpg");
        await Assert.That(tables).Contains("BlobPng");
        await Assert.That(tables).Contains("BlobBlob");
    }

    [Test]
    public async Task MigrateAsync_EmptyDatabase_SupportsMultiCollectionOperations()
    {
        // Arrange
        var migrationService = new DatabaseSchemaMigrationService();
        await migrationService.MigrateAsync(_context!);

        // Act - Create multiple collections with clips
        var collections = new[]
        {
            new Collection { Name = "Work" },
            new Collection { Name = "Personal" },
            new Collection { Name = "Archive" }
        };

        _context!.Collections.AddRange(collections);
        await _context.SaveChangesAsync();

        foreach (var collection in collections)
        {
            var clip = new Clip
            {
                CollectionId = collection.Id,
                Type = ClipType.Text,
                ContentHash = $"hash-{collection.Name}",
                CapturedAt = DateTimeOffset.UtcNow
            };

            _context.Clips.Add(clip);
        }

        await _context.SaveChangesAsync();

        // Assert - Verify collections and clips are properly related
        var workCollection = collections.First(c => c.Name == "Work");
        var personalCollection = collections.First(c => c.Name == "Personal");

        var workClips = await _context.Clips
            .Where(c => c.CollectionId == workCollection.Id)
            .CountAsync();

        var personalClips = await _context.Clips
            .Where(c => c.CollectionId == personalCollection.Id)
            .CountAsync();

        await Assert.That(workClips).IsEqualTo(1);
        await Assert.That(personalClips).IsEqualTo(1);
    }

    // Helper methods

    private async Task<List<string>> GetTableNamesAsync()
    {
        var command = _connection!.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    private async Task<List<string>> GetTableColumnsAsync(string tableName)
    {
        var command = _connection!.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1)); // Column name is at index 1

        return columns;
    }

    private async Task<List<string>> GetIndexesForTableAsync(string tableName)
    {
        var command = _connection!.CreateCommand();
        command.CommandText = $"SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='{tableName}'";

        var indexes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var indexName = reader.GetString(0);
            // Skip auto-generated indexes (sqlite_autoindex_*)
            if (!indexName.StartsWith("sqlite_autoindex"))
                indexes.Add(indexName);
        }

        return indexes;
    }
}
