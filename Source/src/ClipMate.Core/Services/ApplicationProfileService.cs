using ClipMate.Core.Models;
using Microsoft.Extensions.Logging;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing application-specific clipboard format capture profiles.
/// </summary>
public class ApplicationProfileService : IApplicationProfileService
{
    // Smart defaults: formats that should be captured by default
    // IMPORTANT: These must match the format names returned by ClipboardFormatEnumerator
    // which uses StandardFormatNames with "CF_" prefix for standard formats
    private static readonly Dictionary<string, bool> _smartDefaults = new()
    {
        ["CF_TEXT"] = true, // Formats.Text but with CF_ prefix from StandardFormatNames
        ["CF_UNICODETEXT"] = true, // Formats.UnicodeText with CF_ prefix
        ["CF_BITMAP"] = true, // Formats.Bitmap with CF_ prefix
        ["CF_DIB"] = true, // Formats.Dib with CF_ prefix (also bitmap)
        ["CF_DIBV5"] = true, // Formats.DibV5 with CF_ prefix (also bitmap)
        ["CF_HDROP"] = true, // Formats.HDrop with CF_ prefix
        ["HTML Format"] = true, // HTML Format (custom format name, not CF_ prefixed)
        ["Rich Text Format"] = true, // Rich Text Format (custom format name) - captures formatted text from VS, Office, etc.
        ["DataObject"] = false,
        ["CF_LOCALE"] = false, // Formats.Locale with CF_ prefix
        ["OlePrivateData"] = false,
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

        // Check if format is in profile and enabled
        if (profile.Formats.TryGetValue(formatName, out var enabled))
            return enabled;

        // Backward compatibility: if format name starts with "CF_", also try without prefix
        // This handles old profiles that stored "BITMAP" vs new enumeration returning "CF_BITMAP"
        if (formatName.StartsWith("CF_", StringComparison.Ordinal))
        {
            var withoutPrefix = formatName[3..]; // Remove "CF_"
            if (profile.Formats.TryGetValue(withoutPrefix, out enabled))
                return enabled;
        }

        // Special case: All bitmap formats should be treated as equivalent
        // If any bitmap format is enabled, allow all bitmap formats (CF_BITMAP, CF_DIB, CF_DIBV5)
        if (formatName is "CF_BITMAP" or "CF_DIB" or "CF_DIBV5")
        {
            // Check if any bitmap variant is enabled in the profile
            return profile.Formats.TryGetValue("CF_BITMAP", out enabled) && enabled
                   || profile.Formats.TryGetValue("CF_DIB", out enabled) && enabled
                   || profile.Formats.TryGetValue("CF_DIBV5", out enabled) && enabled
                   || profile.Formats.TryGetValue("BITMAP", out enabled) && enabled // Old format without prefix
                   || profile.Formats.TryGetValue("DIB", out enabled) && enabled; // Old format without prefix
        }

        // Format not in profile, default to false (don't capture)
        return false;
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
            Formats = new Dictionary<string, bool>(_smartDefaults),
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
