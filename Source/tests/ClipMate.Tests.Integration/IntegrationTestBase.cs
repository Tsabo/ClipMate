using ClipMate.Data;
using Microsoft.EntityFrameworkCore;
using TUnit.Core;

namespace ClipMate.Tests.Integration;

/// <summary>
/// Base class for integration tests with real database setup.
/// Uses in-memory SQLite database for isolated test execution.
/// </summary>
public abstract class IntegrationTestBase
{
    protected ClipMateDbContext DbContext { get; private set; } = null!;

    [Before(Test)]
    public async Task SetupAsync()
    {
        // Use in-memory SQLite database for fast, isolated tests
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        DbContext = new ClipMateDbContext(options);
        
        // Ensure database is created for each test
        await DbContext.Database.OpenConnectionAsync();
        await DbContext.Database.EnsureCreatedAsync();
    }

    [After(Test)]
    public async Task CleanupAsync()
    {
        if (DbContext != null)
        {
            await DbContext.DisposeAsync();
        }
    }
}
