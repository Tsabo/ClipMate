namespace ClipMate.Core.ValueObjects;

/// <summary>
/// Represents a ClipMate application version from GitHub releases.
/// </summary>
/// <param name="TagName">Git tag name (e.g., "v1.0.0")</param>
/// <param name="Version">Semantic version (e.g., "1.0.0")</param>
/// <param name="ReleaseUrl">URL to the GitHub release page</param>
/// <param name="PublishedAt">Release publication date</param>
/// <param name="IsPrerelease">Whether this is a pre-release version</param>
public record ApplicationVersion(
    string TagName,
    string Version,
    string ReleaseUrl,
    DateTimeOffset PublishedAt,
    bool IsPrerelease)
{
    /// <summary>
    /// Compares this version to the current application version to determine if it's newer.
    /// Supports semantic versioning with pre-release suffixes (e.g., "1.0.0-alpha.3").
    /// </summary>
    /// <param name="currentVersion">Current application version string (e.g., "1.0.0" or "1.0.0-alpha.2")</param>
    /// <returns>True if this version is newer than the current version</returns>
    public bool IsNewerThan(string currentVersion)
    {
        // Parse both versions, stripping pre-release suffixes for System.Version comparison
        var (thisCore, thisPrerelease) = ParseSemanticVersion(Version);
        var (currentCore, currentPrerelease) = ParseSemanticVersion(currentVersion);

        if (thisCore == null || currentCore == null)
            return false;

        // Compare core versions (major.minor.patch)
        var comparison = thisCore.CompareTo(currentCore);

        if (comparison > 0)
            return true; // This version is definitively newer

        if (comparison < 0)
            return false; // This version is definitively older

        // Core versions are equal - compare pre-release tags
        // Per semver spec: 1.0.0-alpha < 1.0.0 (release is newer than pre-release)
        if (string.IsNullOrEmpty(thisPrerelease) && !string.IsNullOrEmpty(currentPrerelease))
            return true; // This is a release, current is pre-release

        if (!string.IsNullOrEmpty(thisPrerelease) && string.IsNullOrEmpty(currentPrerelease))
            return false; // This is pre-release, current is a release

        // Both are pre-releases - compare pre-release identifiers
        if (!string.IsNullOrEmpty(thisPrerelease) && !string.IsNullOrEmpty(currentPrerelease))
            return ComparePrereleaseVersions(thisPrerelease, currentPrerelease) > 0;

        // Both are releases with same core version
        return false;
    }

    /// <summary>
    /// Compares two pre-release version strings according to SemVer specification.
    /// Pre-release versions are compared by splitting on dots and comparing each identifier.
    /// Numeric identifiers are compared as integers, alphanumeric identifiers lexicographically.
    /// Per SemVer: alpha &lt; beta &lt; rc &lt; [no suffix = release]
    /// </summary>
    /// <param name="prerelease1">First pre-release string (e.g., "alpha.11", "beta.1")</param>
    /// <param name="prerelease2">Second pre-release string</param>
    /// <returns>Positive if prerelease1 is newer, negative if older, 0 if equal</returns>
    private static int ComparePrereleaseVersions(string prerelease1, string prerelease2)
    {
        var parts1 = prerelease1.Split('.');
        var parts2 = prerelease2.Split('.');

        var maxLength = Math.Max(parts1.Length, parts2.Length);

        for (var i = 0; i < maxLength; i++)
        {
            // If one version has fewer parts, it's considered less than the longer one
            if (i >= parts1.Length)
                return -1;

            if (i >= parts2.Length)
                return 1;

            var part1 = parts1[i];
            var part2 = parts2[i];

            // Try to parse as integers
            var isNum1 = int.TryParse(part1, out var num1);
            var isNum2 = int.TryParse(part2, out var num2);

            if (isNum1 && isNum2)
            {
                // Both numeric - compare as integers
                var numComparison = num1.CompareTo(num2);
                if (numComparison != 0)
                    return numComparison;
            }
            else if (isNum1)
            {
                // Numeric identifiers always have lower precedence than non-numeric
                return -1;
            }
            else if (isNum2)
            {
                // Numeric identifiers always have lower precedence than non-numeric
                return 1;
            }
            else
            {
                // Both alphanumeric - compare lexicographically (ASCII sort order)
                var strComparison = string.CompareOrdinal(part1, part2);
                if (strComparison != 0)
                    return strComparison;
            }
        }

        // All parts are equal
        return 0;
    }

    /// <summary>
    /// Parses a semantic version string into core version and pre-release suffix.
    /// </summary>
    /// <param name="version">Version string like "1.0.0" or "1.0.0-alpha.3" or "1.0.0+build123"</param>
    /// <returns>Tuple of (core System.Version, prerelease string)</returns>
    private static (Version? Core, string? Prerelease) ParseSemanticVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
            return (null, null);

        // Per semver spec: version format is MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
        // First, strip build metadata (everything after '+')
        var versionWithoutBuild = version.Split('+', 2)[0];

        // Split on '-' to separate core version from pre-release suffix
        var parts = versionWithoutBuild.Split('-', 2);
        var coreVersion = parts[0];
        var prerelease = parts.Length > 1
            ? parts[1]
            : null;

        return System.Version.TryParse(coreVersion, out var parsedCore)
            ? (parsedCore, prerelease)
            : (null, null);
    }

    /// <summary>
    /// Parses a version string from a git tag (removes 'v' prefix if present).
    /// </summary>
    public static string ParseVersionFromTag(string tagName)
    {
        if (string.IsNullOrEmpty(tagName))
            return string.Empty;

        return tagName.StartsWith('v') || tagName.StartsWith('V')
            ? tagName[1..]
            : tagName;
    }
}
