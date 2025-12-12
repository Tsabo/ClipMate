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
            _logger.LogDebug("TOML file length: {Length} chars", tomlContent.Length);

            try
            {
                Configuration = Toml.ToModel<ClipMateConfiguration>(tomlContent);
                _logger.LogInformation("Configuration parsed successfully");
                _logger.LogInformation("MonacoEditor.EnableDebug after parse: {EnableDebug}", Configuration.MonacoEditor.EnableDebug);
                _logger.LogInformation("MonacoEditor.Theme after parse: {Theme}", Configuration.MonacoEditor.Theme);

                return Configuration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse configuration file. Using defaults.");
                Configuration = CreateDefaultConfiguration();

                return Configuration;
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
        if (database == null)
            throw new ArgumentNullException(nameof(database));
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
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
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
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        var config = new ClipMateConfiguration
        {
            Version = 1,
            Preferences = new PreferencesConfiguration(),
            Hotkeys = new HotkeyConfiguration(),
            MonacoEditor = new MonacoEditorConfiguration(),
            DefaultDatabase = "MyClips",
            Databases =
            {
                // Add default database
                ["MyClips"] = new DatabaseConfiguration
                {
                    Name = "My Clips",
                    Directory = appDataPath,
                    AutoLoad = true,
                    AllowBackup = true,
                    ReadOnly = false,
                    CleanupMethod = 3,
                    PurgeDays = 7,
                    UserName = Environment.UserName,
                    IsRemote = false,
                    MultiUser = false,
                    UseModificationTimeStamp = true,
                },
            },
        };

        return config;
    }
}
