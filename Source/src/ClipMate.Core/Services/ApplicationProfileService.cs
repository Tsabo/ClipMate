using ClipMate.Core.Models;
using Microsoft.Extensions.Logging;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing application-specific clipboard format capture profiles.
/// </summary>
public class ApplicationProfileService : IApplicationProfileService
{
    // Smart defaults: formats that should be captured by default
    private static readonly Dictionary<string, bool> _smartDefaults = new()
    {
        [Formats.Text.Name] = true,
        [Formats.UnicodeText.Name] = true,
        [Formats.Bitmap.Name] = true,
        [Formats.HDrop.Name] = true,
        [Formats.Html.Name] = true,
        [Formats.RichText.Name] = false,
        ["DataObject"] = false,
        [Formats.Locale.Name] = false,
        ["OlePrivateData"] = false
    };

    private readonly ILogger<ApplicationProfileService> _logger;
    private readonly IApplicationProfileStore _store;
    private bool _isEnabled;

    public ApplicationProfileService(IApplicationProfileStore store, ILogger<ApplicationProfileService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _isEnabled = true; // Session-only, defaults to enabled (disable for troubleshooting)
    }

    /// <inheritdoc />
    public bool IsApplicationProfilesEnabled() => _isEnabled;

    /// <inheritdoc />
    public void SetApplicationProfilesEnabled(bool enabled)
    {
        _isEnabled = enabled;
        _logger.LogInformation("Application profiles {Status}", enabled
            ? "enabled"
            : "disabled");
    }

    /// <inheritdoc />
    public async Task<bool> ShouldCaptureFormatAsync(string applicationName, string formatName, CancellationToken cancellationToken = default)
    {
        // If profiles are disabled, don't capture
        if (!_isEnabled)
            return false;

        var normalizedAppName = NormalizeApplicationName(applicationName);
        var profile = await GetOrCreateProfileAsync(normalizedAppName, cancellationToken);

        // If application is disabled, don't capture
        if (!profile.Enabled)
            return false;

        // Check if format is in profile and enabled, if not in profile, don't capture
        return profile.Formats.GetValueOrDefault(formatName, false);
    }

    /// <inheritdoc />
    public async Task<ApplicationProfile> GetOrCreateProfileAsync(string applicationName, CancellationToken cancellationToken = default)
    {
        var normalizedAppName = NormalizeApplicationName(applicationName);
        var profile = await _store.GetProfileAsync(normalizedAppName, cancellationToken);

        if (profile != null)
            return profile;

        // Create new profile with smart defaults
        profile = new ApplicationProfile
        {
            ApplicationName = normalizedAppName,
            Enabled = true,
            Formats = new Dictionary<string, bool>(_smartDefaults)
        };

        await _store.AddOrUpdateProfileAsync(profile, cancellationToken);
        _logger.LogInformation("Created new application profile for {ApplicationName} with smart defaults", normalizedAppName);

        return profile;
    }

    /// <inheritdoc />
    public async Task UpdateProfileAsync(ApplicationProfile profile, CancellationToken cancellationToken = default) => await _store.AddOrUpdateProfileAsync(profile, cancellationToken);

    /// <inheritdoc />
    public async Task<Dictionary<string, ApplicationProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) => await _store.LoadProfilesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task DeleteProfileAsync(string applicationName, CancellationToken cancellationToken = default)
    {
        var normalizedAppName = NormalizeApplicationName(applicationName);
        await _store.DeleteProfileAsync(normalizedAppName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAllProfilesAsync(CancellationToken cancellationToken = default) => await _store.DeleteAllProfilesAsync(cancellationToken);

    /// <inheritdoc />
    public string NormalizeApplicationName(string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
            return string.Empty;

        var normalized = applicationName.Trim().ToUpperInvariant();

        // Remove .EXE extension if present
        if (normalized.EndsWith(".EXE", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];

        return normalized;
    }
}
