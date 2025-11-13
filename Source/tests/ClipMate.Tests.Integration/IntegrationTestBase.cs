using ClipMate.Data;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Tests.Integration;

/// <summary>
/// Base class for integration tests with real database setup.
/// Uses in-memory SQLite database for isolated test execution.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected ClipMateDbContext DbContext { get; private set; }

    protected IntegrationTestBase()
    {
        // Use in-memory SQLite database for fast, isolated tests
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        DbContext = new ClipMateDbContext(options);
        
        // Ensure database is created for each test
        DbContext.Database.OpenConnection();
        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            DbContext.Dispose();
        }
    }
}
