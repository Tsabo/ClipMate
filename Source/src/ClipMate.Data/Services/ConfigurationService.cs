using System.Text;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;
using Tomlyn;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing application configuration using TOML format.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ILogger<ConfigurationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationService" /> class.
    /// </summary>
    /// <param name="configurationDirectory">Directory where configuration file is stored.</param>
    /// <param name="logger">Logger instance.</param>
    public ConfigurationService(string configurationDirectory, ILogger<ConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(configurationDirectory))
            throw new ArgumentException("Configuration directory cannot be null or empty.", nameof(configurationDirectory));

        // Ensure directory exists
        Directory.CreateDirectory(configurationDirectory);

        ConfigurationFilePath = Path.Join(configurationDirectory, "clipmate.toml");
        Configuration = CreateDefaultConfiguration();
    }

    /// <inheritdoc />
    public ClipMateConfiguration Configuration { get; private set; }

    /// <inheritdoc />
    public string ConfigurationFilePath { get; }

    /// <inheritdoc />
    public async Task<ClipMateConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(ConfigurationFilePath))
            {
                _logger.LogInformation("Configuration file not found at {Path}. Creating default configuration.", ConfigurationFilePath);
                Configuration = CreateDefaultConfiguration();
                await SaveInternalAsync(cancellationToken);

                return Configuration;
            }

            _logger.LogInformation("Loading configuration from {Path}", ConfigurationFilePath);
            var tomlContent = await File.ReadAllTextAsync(ConfigurationFilePath, cancellationToken);

            try
            {
                Configuration = Toml.ToModel<ClipMateConfiguration>(tomlContent, options: new TomlModelOptions
                {
                    IgnoreMissingProperties = true,
                });

                _logger.LogInformation("Configuration parsed successfully");
                _logger.LogInformation("MonacoEditor.EnableDebug after parse: {EnableDebug}", Configuration.MonacoEditor.EnableDebug);
                _logger.LogInformation("MonacoEditor.Theme after parse: {Theme}", Configuration.MonacoEditor.Theme);

                // Validate the configuration
                var validationErrors = ValidateConfiguration(Configuration);
                if (validationErrors.Count <= 0)
                    return Configuration;

                _logger.LogError("Configuration validation failed with {Count} error(s):", validationErrors.Count);
                foreach (var error in validationErrors)
                    _logger.LogError("  - {Error}", error);

                throw new InvalidOperationException(
                    $"Configuration validation failed:\n{string.Join("\n", validationErrors.ToArray())}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load or validate configuration file");
                throw; // Re-throw to let caller handle the error
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await SaveInternalAsync(cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(ClipMateConfiguration configuration, CancellationToken cancellationToken = default)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Resetting configuration to defaults");
        Configuration = CreateDefaultConfiguration();
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddOrUpdateDatabaseAsync(string databaseId, DatabaseConfiguration database, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databaseId))
            throw new ArgumentException("Database ID cannot be null or empty.", nameof(databaseId));

#pragma warning disable IDE0016
        ArgumentNullException.ThrowIfNull(database);
#pragma warning restore IDE0016

        Configuration.Databases[databaseId] = database;
        _logger.LogInformation("Database configuration '{DatabaseId}' updated", databaseId);
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveDatabaseAsync(string databaseId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databaseId))
            throw new ArgumentException("Database ID cannot be null or empty.", nameof(databaseId));

        if (Configuration.Databases.Remove(databaseId))
        {
            _logger.LogInformation("Database configuration '{DatabaseId}' removed", databaseId);
            await SaveAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task AddOrUpdateApplicationProfileAsync(string applicationName, ApplicationProfile profile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
            throw new ArgumentException("Application name cannot be null or empty.", nameof(applicationName));

#pragma warning disable IDE0016
        ArgumentNullException.ThrowIfNull(profile);
#pragma warning restore IDE0016

        Configuration.ApplicationProfiles[applicationName] = profile;
        _logger.LogInformation("Application profile '{ApplicationName}' updated", applicationName);
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveApplicationProfileAsync(string applicationName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
            throw new ArgumentException("Application name cannot be null or empty.", nameof(applicationName));

        if (Configuration.ApplicationProfiles.Remove(applicationName))
        {
            _logger.LogInformation("Application profile '{ApplicationName}' removed", applicationName);
            await SaveAsync(cancellationToken);
        }
    }

    private async Task SaveInternalAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving configuration to {Path}", ConfigurationFilePath);

        try
        {
            var tomlContent = Toml.FromModel(Configuration);

            // Write to temporary file first, then rename for atomicity
            var tempPath = ConfigurationFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, tomlContent, Encoding.UTF8, cancellationToken);

            // Backup existing file if it exists
            if (File.Exists(ConfigurationFilePath))
            {
                var backupPath = ConfigurationFilePath + ".bak";
                File.Copy(ConfigurationFilePath, backupPath, true);
            }

            // Replace with new file
            File.Move(tempPath, ConfigurationFilePath, true);

            _logger.LogInformation("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration file");

            throw;
        }
    }

    private ClipMateConfiguration CreateDefaultConfiguration()
    {
        // var appDataPath = Path.Combine(
        //     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        //     "ClipMate");

        var config = new ClipMateConfiguration
        {
            Version = 1,
            Preferences = new PreferencesConfiguration(),
            Hotkeys = new HotkeyConfiguration(),
            MonacoEditor = new MonacoEditorConfiguration(),
            /*
            DefaultDatabase = "MyClips",
            Databases =
            {
                // Add default database
                ["MyClips"] = new DatabaseConfiguration
                {
                    Name = "My Clips",
                    FilePath = Path.Combine(appDataPath, "clipmate.db"),
                    AutoLoad = true,
                    AllowBackup = true,
                    ReadOnly = false,
                    CleanupMethod = CleanupMethod.AtStartup,
                    PurgeDays = 7,
                    UserName = Environment.UserName,
                    IsRemote = false,
                    MultiUser = false,
                    UseModificationTimeStamp = true,
                },
            },
            */
        };

        return config;
    }

    private List<string> ValidateConfiguration(ClipMateConfiguration config)
    {
        var errors = new List<string>();

        // Validate databases exist
        if (config.Databases.Count == 0)
            errors.Add("No databases configured. At least one database is required.");
        else
        {
            // Validate each database configuration
            foreach (var item in config.Databases)
            {
                var dbKey = item.Key;
                var db = item.Value;

                if (string.IsNullOrWhiteSpace(db.Name))
                    errors.Add($"Database '{dbKey}': Name is required");

                if (string.IsNullOrWhiteSpace(db.FilePath))
                    errors.Add($"Database '{dbKey}': FilePath is required");
                else if (db.FilePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    errors.Add($"Database '{dbKey}': FilePath contains invalid characters");

                // Validate PurgeDays range
                if (db.PurgeDays < 0)
                    errors.Add($"Database '{dbKey}': PurgeDays must be >= 0");
            }

            // Validate default database exists
            if (!string.IsNullOrWhiteSpace(config.DefaultDatabase))
            {
                if (config.Databases.ContainsKey(config.DefaultDatabase))
                    return errors;

                var availableKeys = string.Join(", ", config.Databases.Keys);
                errors.Add($"Default database '{config.DefaultDatabase}' not found. Available: {availableKeys}");
            }
            else if (config.Databases.Count > 0)
            {
                // Auto-assign first database as default if not specified
                config.DefaultDatabase = config.Databases.Keys.First();
                _logger.LogInformation("No default database specified. Using first database: {Key}", config.DefaultDatabase);
            }
        }

        return errors;
    }
}
