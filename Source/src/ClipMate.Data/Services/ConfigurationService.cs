using ClipMate.Core.Models.Configuration;
using Microsoft.Extensions.Logging;
using Tomlyn;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing application configuration using TOML format.
/// </summary>
public class ConfigurationService : Core.Services.IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configurationFilePath;
    private ClipMateConfiguration _configuration;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
    /// </summary>
    /// <param name="configurationDirectory">Directory where configuration file is stored.</param>
    /// <param name="logger">Logger instance.</param>
    public ConfigurationService(string configurationDirectory, ILogger<ConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (string.IsNullOrWhiteSpace(configurationDirectory))
        {
            throw new ArgumentException("Configuration directory cannot be null or empty.", nameof(configurationDirectory));
        }

        // Ensure directory exists
        System.IO.Directory.CreateDirectory(configurationDirectory);

        _configurationFilePath = System.IO.Path.Combine(configurationDirectory, "clipmate.toml");
        _configuration = CreateDefaultConfiguration();
    }

    /// <inheritdoc/>
    public ClipMateConfiguration Configuration => _configuration;

    /// <inheritdoc/>
    public string ConfigurationFilePath => _configurationFilePath;

    /// <inheritdoc/>
    public async Task<ClipMateConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!System.IO.File.Exists(_configurationFilePath))
            {
                _logger.LogInformation("Configuration file not found at {Path}. Creating default configuration.", _configurationFilePath);
                _configuration = CreateDefaultConfiguration();
                await SaveInternalAsync(cancellationToken);
                return _configuration;
            }

            _logger.LogInformation("Loading configuration from {Path}", _configurationFilePath);
            var tomlContent = await System.IO.File.ReadAllTextAsync(_configurationFilePath, cancellationToken);

            try
            {
                _configuration = Toml.ToModel<ClipMateConfiguration>(tomlContent);
                _logger.LogInformation("Configuration loaded successfully");
                return _configuration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse configuration file. Using defaults.");
                _configuration = CreateDefaultConfiguration();
                return _configuration;
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task SaveAsync(ClipMateConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Resetting configuration to defaults");
        _configuration = CreateDefaultConfiguration();
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddOrUpdateDatabaseAsync(string databaseId, DatabaseConfiguration database, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            throw new ArgumentException("Database ID cannot be null or empty.", nameof(databaseId));
        }

        if (database == null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        _configuration.Databases[databaseId] = database;
        _logger.LogInformation("Database configuration '{DatabaseId}' updated", databaseId);
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveDatabaseAsync(string databaseId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            throw new ArgumentException("Database ID cannot be null or empty.", nameof(databaseId));
        }

        if (_configuration.Databases.Remove(databaseId))
        {
            _logger.LogInformation("Database configuration '{DatabaseId}' removed", databaseId);
            await SaveAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task AddOrUpdateApplicationProfileAsync(string applicationName, ApplicationProfile profile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            throw new ArgumentException("Application name cannot be null or empty.", nameof(applicationName));
        }

        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        _configuration.ApplicationProfiles[applicationName] = profile;
        _logger.LogInformation("Application profile '{ApplicationName}' updated", applicationName);
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveApplicationProfileAsync(string applicationName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            throw new ArgumentException("Application name cannot be null or empty.", nameof(applicationName));
        }

        if (_configuration.ApplicationProfiles.Remove(applicationName))
        {
            _logger.LogInformation("Application profile '{ApplicationName}' removed", applicationName);
            await SaveAsync(cancellationToken);
        }
    }

    private async Task SaveInternalAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving configuration to {Path}", _configurationFilePath);

        try
        {
            var tomlContent = Toml.FromModel(_configuration);
            
            // Write to temporary file first, then rename for atomicity
            var tempPath = _configurationFilePath + ".tmp";
            await System.IO.File.WriteAllTextAsync(tempPath, tomlContent, System.Text.Encoding.UTF8, cancellationToken);
            
            // Backup existing file if it exists
            if (System.IO.File.Exists(_configurationFilePath))
            {
                var backupPath = _configurationFilePath + ".bak";
                System.IO.File.Copy(_configurationFilePath, backupPath, overwrite: true);
            }

            // Replace with new file
            System.IO.File.Move(tempPath, _configurationFilePath, overwrite: true);
            
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
        var appDataPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        var config = new ClipMateConfiguration
        {
            Version = 1,
            Preferences = new PreferencesConfiguration(),
            Hotkeys = new HotkeyConfiguration(),
            DefaultDatabase = "MyClips"
        };

        // Add default database
        config.Databases["MyClips"] = new DatabaseConfiguration
        {
            Name = "My Clips",
            Directory = appDataPath,
            AutoLoad = true,
            AllowBackup = true,
            ReadOnly = false,
            CleanupMethod = 3,
            PurgeDays = 7,
            UserName = System.Environment.UserName,
            IsRemote = false,
            MultiUser = false,
            UseModificationTimeStamp = true
        };

        return config;
    }
}
