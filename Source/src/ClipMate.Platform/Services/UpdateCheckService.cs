using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for checking for ClipMate application updates from GitHub releases.
/// </summary>
public class UpdateCheckService : IUpdateCheckService
{
    private const string GitHubReleasesUrl = "https://api.github.com/repos/clipmate/ClipMate/releases";
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateCheckService> _logger;

    public UpdateCheckService(HttpClient httpClient, ILogger<UpdateCheckService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // GitHub API requires a User-Agent header
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ClipMate-UpdateChecker");
    }

    public async Task<ApplicationVersion?> CheckForUpdatesAsync(string currentVersion,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for updates. Current version: {CurrentVersion}", currentVersion);

            var releases = await _httpClient.GetFromJsonAsync<GitHubRelease[]>(
                GitHubReleasesUrl,
                cancellationToken);

            if (releases == null || releases.Length == 0)
            {
                _logger.LogInformation("No releases found on GitHub");
                return null;
            }

            // Filter out pre-releases if not requested
            var eligibleReleases = includePrerelease
                ? releases
                : releases.Where(r => !r.Prerelease).ToArray();

            if (eligibleReleases.Length == 0)
            {
                _logger.LogInformation("No eligible releases found");
                return null;
            }

            // Find the first release that is newer than the current version
            foreach (var release in eligibleReleases)
            {
                var version = ApplicationVersion.ParseVersionFromTag(release.TagName);
                var appVersion = new ApplicationVersion(
                    release.TagName,
                    version,
                    release.HtmlUrl,
                    release.PublishedAt,
                    release.Prerelease);

                if (!appVersion.IsNewerThan(currentVersion))
                    continue;

                _logger.LogInformation(
                    "Found newer version: {NewVersion} (published: {PublishedAt})",
                    appVersion.Version,
                    appVersion.PublishedAt);

                return appVersion;
            }

            _logger.LogInformation("No newer version available");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while checking for updates");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return null;
        }
    }

    /// <summary>
    /// DTO for GitHub release API response.
    /// </summary>
    private record GitHubRelease(
        [property: JsonPropertyName("tag_name")]
        string TagName,
        [property: JsonPropertyName("html_url")]
        string HtmlUrl,
        [property: JsonPropertyName("published_at")]
        DateTimeOffset PublishedAt,
        [property: JsonPropertyName("prerelease")]
        bool Prerelease);
}
