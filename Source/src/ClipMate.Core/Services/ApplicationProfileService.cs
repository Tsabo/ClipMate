using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing application-specific clipboard format capture profiles, which work as ClipMate 7's do: every
/// Source Application gets a profile the first time it is captured, and each Format it offers is decided once.
/// </summary>
public class ApplicationProfileService : IApplicationProfileService
{
    // Profile names of the Formats ClipMate 7's rules mention; every image Format follows the one image setting
    private const string _text = "CF_TEXT";
    private const string _image = "CF_DIB";
    private const string _files = "CF_HDROP";
    private const string _fileName = "FileName";
    private const string _html = "HTML Format";
    private const string _rtf = "Rich Text Format";

    // ClipMate 7's profiles for programs it knows (0x8bf228); a name is matched exactly or, with Contains, anywhere
    private static readonly (string Name, bool Contains, (string Format, bool On)[] Formats)[] _presets =
    [
        ("EXPLORER", false, [(_text, true), (_files, true), (_fileName, true), (_image, true), (_html, true), (_rtf, false)]),
        ("IEXPLORE", false, [(_text, true), (_files, true), (_fileName, true), (_image, true), (_html, true), (_rtf, false)]),
        ("EUDORA", true, [(_text, true), (_html, false), (_rtf, false)]),
        ("NETCAPTOR", false, [(_text, true), (_image, true), (_html, true), (_rtf, false)]),
        ("MAXTHON", true, [(_text, true), (_image, true), (_html, true), (_rtf, false)]),
        ("MYIE2", true, [(_text, true), (_image, true), (_html, true), (_rtf, false)]),
        ("NETSCAPE", true, [(_text, true), (_image, true), (_html, true), (_rtf, false)]),
        ("MOZILLA", true, [(_text, true), (_image, true), (_html, true), (_rtf, false)]),
        ("FIREBIRD", true, [(_text, true), (_image, true), (_html, true), (_rtf, false)]),
        ("FIREFOX", true, [(_text, true), (_image, true), (_html, true), (_rtf, false)]),
        ("PSP", false, [(_text, true), (_image, true), ("Jasc Paint Shop Pro 6.0 Clipboard Format", true)]),
        ("DELPHI32", false, [(_text, true), ("Delphi Components", true)]),
        ("WINDOWS", false, [(_text, true), (_image, true)]),
        ("WORDPERFECT", false, [(_text, true), (_rtf, true)]),
        ("SMARTDRAW", true, [(_text, true)]),
        ("EXCEL", true, [(_text, true), (_rtf, false), (_image, false), (_html, false)]),
        ("ACCESS", true, [(_text, true), (_rtf, false), (_html, false)]),
        ("WINWORD", true, [(_text, true), (_rtf, true), (_image, false), (_html, false)]),
    ];

    // The decode loop and a manual capture can learn formats into the same profile at once
    private readonly SemaphoreSlim _learnLock = new(1, 1);

    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ApplicationProfileService> _logger;
    private readonly IApplicationProfileStore _store;

    public ApplicationProfileService(IApplicationProfileStore store, IConfigurationService configurationService,
        ILogger<ApplicationProfileService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsApplicationProfilesEnabled() => _configurationService.Configuration.Preferences.ApplicationProfilesEnabled;

    /// <inheritdoc />
    public void SetApplicationProfilesEnabled(bool enabled)
    {
        _configurationService.Configuration.Preferences.ApplicationProfilesEnabled = enabled;
        _logger.LogInformation("Application profiles {Status}", enabled
            ? "enabled"
            : "disabled");
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>?> ApplyProfileAsync(string applicationName, IReadOnlyCollection<string> formatNames, bool evenWhenSwitchedOff = false,
        CancellationToken cancellationToken = default)
    {
        var all = formatNames.ToHashSet(StringComparer.Ordinal);
        if (!IsApplicationProfilesEnabled())
            return all;

        var normalizedAppName = NormalizeApplicationName(applicationName);
        await _learnLock.WaitAsync(cancellationToken);
        try
        {
            return await ApplyProfileLockedAsync(normalizedAppName, all, evenWhenSwitchedOff, cancellationToken);
        }
        finally
        {
            _learnLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateProfileAsync(ApplicationProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // Keyed by the same name every other call looks it up by
        profile.ApplicationName = NormalizeApplicationName(profile.ApplicationName);
        await _store.AddOrUpdateProfileAsync(profile, cancellationToken);
    }

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

    private async Task<IReadOnlySet<string>?> ApplyProfileLockedAsync(string normalizedAppName, HashSet<string> all, bool evenWhenSwitchedOff,
        CancellationToken cancellationToken)
    {
        var profile = await _store.GetProfileAsync(normalizedAppName, cancellationToken);
        var changed = false;
        if (profile == null)
        {
            profile = CreateProfile(normalizedAppName, all);
            changed = true;
            _logger.LogInformation("Created application profile {ApplicationName}", normalizedAppName);
        }

        if (!profile.Enabled && !evenWhenSwitchedOff)
        {
            if (changed)
                await _store.AddOrUpdateProfileAsync(profile, cancellationToken);

            return null;
        }

        var captured = new HashSet<string>(StringComparer.Ordinal);
        foreach (var formatName in all)
        {
            if (TryGetSetting(profile, formatName) is not { } enabled)
            {
                enabled = DecideNewFormat(formatName, all);
                profile.Formats[formatName] = enabled;
                changed = true;
                _logger.LogInformation("Application profile {ApplicationName} learned format {FormatName} ({Setting})",
                    normalizedAppName, formatName, enabled
                        ? "on"
                        : "off");
            }

            if (enabled)
                captured.Add(formatName);
        }

        if (changed)
            await _store.AddOrUpdateProfileAsync(profile, cancellationToken);

        return captured;
    }

    /// <summary>
    /// A new profile: ClipMate 7's preset for a program it knows, otherwise HTML if offered, and text if offered or else
    /// the image if offered. Formats it doesn't set are decided as they are met.
    /// </summary>
    private static ApplicationProfile CreateProfile(string applicationName, HashSet<string> offered)
    {
        var profile = new ApplicationProfile { ApplicationName = applicationName };
        var preset = _presets.FirstOrDefault(p => p.Contains
            ? applicationName.Contains(p.Name, StringComparison.OrdinalIgnoreCase)
            : applicationName.Equals(p.Name, StringComparison.OrdinalIgnoreCase));

        if (preset.Formats != null)
        {
            foreach (var (format, on) in preset.Formats)
                profile.Formats[format] = on;

            return profile;
        }

        if (offered.Contains(_html))
            profile.Formats[_html] = true;

        if (offered.Contains(_text))
            profile.Formats[_text] = true;
        else if (offered.Contains(_image))
            profile.Formats[_image] = true;

        return profile;
    }

    /// <summary>
    /// ClipMate 7's setting for a Format a profile meets for the first time: text and RTF on, everything else off.
    /// </summary>
    private static bool DecideNewFormat(string formatName, HashSet<string> offered) => formatName switch
    {
        _text or _rtf => true,
        "Ami Text Format" => offered.Contains(_text),
        var _ => false,
    };

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
}
