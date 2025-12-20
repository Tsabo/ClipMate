using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Factory implementation for creating IClipRepository instances.
/// Resolves database contexts via IDatabaseManager and constructs repositories with proper dependencies.
/// </summary>
public class ClipRepositoryFactory : IClipRepositoryFactory
{
    private readonly IDatabaseManager _databaseManager;
    private readonly ILogger<ClipRepository> _logger;

    public ClipRepositoryFactory(IDatabaseManager databaseManager,
        ILogger<ClipRepository> logger)
    {
        _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IClipRepository CreateRepository(string databaseKey)
    {
        if (string.IsNullOrWhiteSpace(databaseKey))
            throw new ArgumentException("Database key cannot be null or empty", nameof(databaseKey));

        var context = _databaseManager.GetDatabaseContext(databaseKey);
        return context == null
            ? throw new InvalidOperationException($"Database context for key '{databaseKey}' not found. Ensure the database is loaded.")
            : new ClipRepository(context, _logger);
    }
}
