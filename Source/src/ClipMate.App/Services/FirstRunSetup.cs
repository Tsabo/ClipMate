using System.IO;
using ClipMate.Core.Helpers;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services;

/// <summary>
/// Creates the first database and the configuration that points at it: what the setup wizard does once the user has
/// chosen a name and location, and what an isolated copy (started with <c>--data-dir</c>) does on its own.
/// </summary>
public static class FirstRunSetup
{
    /// <summary>
    /// The name of the database a first run creates.
    /// </summary>
    public const string DefaultDatabaseName = "My Clips";

    /// <summary>
    /// Creates the default database inside <see cref="ClipMatePaths.DataDirectory" /> and its configuration, without
    /// asking the user anything.
    /// </summary>
    /// <returns>The new database file.</returns>
    public static async Task<string> CreateDefaultAsync(ISetupService setupService, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setupService);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var databaseDirectory = Path.Combine(ClipMatePaths.DataDirectory, "Databases");
        Directory.CreateDirectory(databaseDirectory);

        var databasePath = Path.Combine(databaseDirectory, "clipmate.db");
        if (!await setupService.CreateDatabaseAsync(databasePath, cancellationToken))
            throw new InvalidOperationException($"Failed to create the database at {databasePath}.");

        await setupService.SeedDefaultDataAsync(databasePath, cancellationToken);
        await SaveDatabaseConfigurationAsync(ClipMatePaths.DataDirectory, DefaultDatabaseName, databaseDirectory, loggerFactory, cancellationToken);
        return databasePath;
    }

    /// <summary>
    /// Adds a database named <paramref name="databaseName" /> in <paramref name="databaseDirectory" /> to the
    /// configuration in <paramref name="configurationDirectory" /> and makes it the default database.
    /// </summary>
    public static async Task SaveDatabaseConfigurationAsync(string configurationDirectory,
        string databaseName,
        string databaseDirectory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var configService = new ConfigurationService(configurationDirectory, loggerFactory.CreateLogger<ConfigurationService>());
        var config = await configService.LoadAsync(cancellationToken);

        // Same key pattern as DatabaseOptionsViewModel
        var databaseKey = $"db_{Guid.NewGuid().ToString("N")[..8]}";
        config.Databases[databaseKey] = new DatabaseConfiguration
        {
            Name = databaseName,
            FilePath = Path.Combine(databaseDirectory, "clipmate.db"),
            AutoLoad = true,
            AllowBackup = true,
            ReadOnly = false,
            CleanupMethod = CleanupMethod.AtStartup,
            PurgeDays = 7,
        };

        config.DefaultDatabase = databaseKey;
        await configService.SaveAsync(config, cancellationToken);
    }
}
