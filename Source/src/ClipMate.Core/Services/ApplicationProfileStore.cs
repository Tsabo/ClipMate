using ClipMate.Core.Models;
using Microsoft.Extensions.Logging;
using Tomlyn;
using Tomlyn.Model;

namespace ClipMate.Core.Services;

/// <summary>
/// Stores and retrieves application profiles using TOML format.
/// Manages the %LOCALAPPDATA%\ClipMate\application-profiles.toml file.
/// </summary>
public class ApplicationProfileStore : IApplicationProfileStore
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly string _filePath;
    private readonly ILogger<ApplicationProfileStore> _logger;

    public ApplicationProfileStore(string filePath, ILogger<ApplicationProfileStore> logger)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads all application profiles from the TOML file.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of application profiles keyed by application name.</returns>
    public async Task<Dictionary<string, ApplicationProfile>> LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("Application profiles file does not exist: {FilePath}", _filePath);

                return new Dictionary<string, ApplicationProfile>();
            }

            var tomlContent = await File.ReadAllTextAsync(_filePath, cancellationToken);
            var tomlModel = Toml.ToModel(tomlContent);

            var profiles = new Dictionary<string, ApplicationProfile>();

            if (tomlModel.TryGetValue("profiles", out var profilesObj) && profilesObj is TomlTable profilesTable)
            {
                foreach (var (appName, profileObj) in profilesTable)
                {
                    if (profileObj is TomlTable profileTable)
                    {
                        var profile = ParseProfile(appName, profileTable);
                        profiles[appName] = profile;
                    }
                }
            }

            _logger.LogInformation("Loaded {Count} application profiles", profiles.Count);

            return profiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading application profiles from {FilePath}", _filePath);

            return new Dictionary<string, ApplicationProfile>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Saves all application profiles to the TOML file.
    /// </summary>
    /// <param name="profiles">Dictionary of profiles to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveProfilesAsync(Dictionary<string, ApplicationProfile> profiles, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Build TOML model
            var tomlModel = new TomlTable();
            var profilesTable = new TomlTable();

            foreach (var (appName, profile) in profiles.OrderBy(p => p.Key))
            {
                var profileTable = new TomlTable
                {
                    ["enabled"] = profile.Enabled
                };

                // Add formats in alphabetical order
                foreach (var (formatName, enabled) in profile.Formats.OrderBy(p => p.Key))
                    profileTable[formatName] = enabled;

                profilesTable[appName] = profileTable;
            }

            tomlModel["profiles"] = profilesTable;

            // Serialize to TOML
            var tomlContent = Toml.FromModel(tomlModel);
            await File.WriteAllTextAsync(_filePath, tomlContent, cancellationToken);

            _logger.LogInformation("Saved {Count} application profiles to {FilePath}", profiles.Count, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving application profiles to {FilePath}", _filePath);

            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Gets a single application profile by name.
    /// </summary>
    /// <param name="applicationName">The application name (normalized).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The profile if found, otherwise null.</returns>
    public async Task<ApplicationProfile?> GetProfileAsync(string applicationName, CancellationToken cancellationToken = default)
    {
        var profiles = await LoadProfilesAsync(cancellationToken);

        return profiles.GetValueOrDefault(applicationName);
    }

    /// <summary>
    /// Adds or updates an application profile.
    /// </summary>
    /// <param name="profile">The profile to add or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AddOrUpdateProfileAsync(ApplicationProfile profile, CancellationToken cancellationToken = default)
    {
        var profiles = await LoadProfilesAsync(cancellationToken);
        profiles[profile.ApplicationName] = profile;
        await SaveProfilesAsync(profiles, cancellationToken);
    }

    /// <summary>
    /// Deletes an application profile by name.
    /// </summary>
    /// <param name="applicationName">The application name to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteProfileAsync(string applicationName, CancellationToken cancellationToken = default)
    {
        var profiles = await LoadProfilesAsync(cancellationToken);
        if (profiles.Remove(applicationName))
        {
            await SaveProfilesAsync(profiles, cancellationToken);
            _logger.LogInformation("Deleted application profile: {ApplicationName}", applicationName);
        }
    }

    /// <summary>
    /// Deletes all application profiles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        await SaveProfilesAsync(new Dictionary<string, ApplicationProfile>(), cancellationToken);
        _logger.LogInformation("Deleted all application profiles");
    }

    /// <summary>
    /// Parses an application profile from a TOML table.
    /// </summary>
    private ApplicationProfile ParseProfile(string appName, TomlTable profileTable)
    {
        var profile = new ApplicationProfile
        {
            ApplicationName = appName,
            Enabled = profileTable.TryGetValue("enabled", out var enabledObj) && enabledObj is true
        };

        // Parse format settings (all entries except "enabled")
        foreach (var (key, value) in profileTable)
        {
            if (key != "enabled" && value is bool formatEnabled)
                profile.Formats[key] = formatEnabled;
        }

        return profile;
    }
}
