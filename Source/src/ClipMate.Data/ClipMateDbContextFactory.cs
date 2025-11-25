using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClipMate.Data;

/// <summary>
/// Design-time factory for creating ClipMateDbContext instances during migrations.
/// </summary>
public class ClipMateDbContextFactory : IDesignTimeDbContextFactory<ClipMateDbContext>
{
    public ClipMateDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        
        // Use a temporary in-memory database path for design-time operations
        optionsBuilder.UseSqlite("Data Source=clipmate.db");

        return new ClipMateDbContext(optionsBuilder.Options);
    }
}
