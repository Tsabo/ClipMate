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

    // The decode loop and a manual capture can learn formats into the same profile at once
    private readonly SemaphoreSlim _learnLock = new(1, 1);
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
    public async Task<IReadOnlySet<string>?> GetCapturedFormatsAsync(string applicationName, IReadOnlyCollection<string> formatNames,
        CancellationToken cancellationToken = default)
    {
        var all = formatNames.ToHashSet(StringComparer.Ordinal);
        if (!_isEnabled)
            return all;

        var normalizedAppName = NormalizeApplicationName(applicationName);
        await _learnLock.WaitAsync(cancellationToken);
        try
        {
            return await GetCapturedFormatsLockedAsync(normalizedAppName, all, cancellationToken);
        }
        finally
        {
            _learnLock.Release();
        }
    }

    private async Task<IReadOnlySet<string>?> GetCapturedFormatsLockedAsync(string normalizedAppName, HashSet<string> all,
        CancellationToken cancellationToken)
    {
        var profile = await _store.GetProfileAsync(normalizedAppName, cancellationToken);
        if (profile == null)
            return all;

        if (!profile.Enabled)
            return null;

        var captured = new HashSet<string>(StringComparer.Ordinal);
        var learned = false;
        foreach (var formatName in all)
        {
            if (TryGetSetting(profile, formatName) is not { } enabled)
            {
                enabled = _smartDefaults.GetValueOrDefault(formatName);
                profile.Formats[formatName] = enabled;
                learned = true;
                _logger.LogInformation("Application profile {ApplicationName} learned format {FormatName} ({Setting})",
                    normalizedAppName, formatName, enabled ? "on" : "off");
            }

            if (enabled)
                captured.Add(formatName);
        }

        if (learned)
            await _store.AddOrUpdateProfileAsync(profile, cancellationToken);

        return captured;
    }

    /// <summary>
    /// The profile's setting for a format, or null when the profile doesn't have it yet.
    /// </summary>
    private static bool? TryGetSetting(ApplicationProfile profile, string formatName)
    {
        if (profile.Formats.TryGetValue(formatName, out var enabled))
            return enabled;

        // Profiles from older versions store the standard formats without the "CF_" prefix
        if (formatName.StartsWith("CF_", StringComparison.Ordinal) && profile.Formats.TryGetValue(formatName[3..], out enabled))
            return enabled;

        // Every bitmap format follows any bitmap setting the profile has
        if (formatName is "CF_BITMAP" or "CF_DIB" or "CF_DIBV5")
        {
            string[] bitmapFormats = ["CF_BITMAP", "CF_DIB", "CF_DIBV5", "BITMAP", "DIB"];
            var settings = bitmapFormats.Where(profile.Formats.ContainsKey).Select(p => profile.Formats[p]).ToList();
            if (settings.Count > 0)
                return settings.Any(p => p);
        }

        return null;
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
