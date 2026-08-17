using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for UpdateCheckService that verify real GitHub API interactions.
/// </summary>
public class UpdateCheckServiceTests : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly Mock<ILogger<UpdateCheckService>> _logger = new();

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private UpdateCheckService CreateService() => new(_httpClient, _logger.Object);

    /// <summary>
    /// UpdateCheckService swallows HTTP failures and returns null, so a null result can mean
    /// either "no update found" or "the GitHub API call failed" (e.g. rate limiting on shared
    /// CI runner IPs). The service logs an error only on the failure path.
    /// </summary>
    private bool LoggedApiFailure() =>
        _logger.Invocations.Any(i =>
            i.Method.Name == nameof(ILogger.Log) &&
            i.Arguments[0] is LogLevel.Error);

    [Test]
    [Category("Integration")]
    [Category("External")]
    public async Task CheckForUpdatesAsync_WithAlphaVersion_FindsBetaUpdate()
    {
        // Arrange
        var service = CreateService();
        const string currentVersion = "0.1.0-alpha.11";

        // Act
        var result = await service.CheckForUpdatesAsync(currentVersion, true);

        // Assert
        Skip.When(result == null && LoggedApiFailure(), "GitHub API request failed (e.g. rate limited) rather than genuinely finding no update");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Version).IsNotEqualTo(currentVersion);

        // Verify we found a beta or stable version
        var isBetaOrStable = result.Version.Contains("-beta") || !result.Version.Contains("-alpha");
        if (!isBetaOrStable)
            throw new InvalidOperationException($"Expected beta or stable version but got: {result.Version}");

        await Assert.That(isBetaOrStable).IsTrue();
    }

    [Test]
    [Category("Integration")]
    [Category("External")]
    public async Task CheckForUpdatesAsync_WithBetaVersion_FindsNewerBeta()
    {
        // Arrange
        var service = CreateService();
        const string currentVersion = "0.1.0-beta.1";

        // Act
        var result = await service.CheckForUpdatesAsync(currentVersion, true);

        // Assert - Either finds a newer version or null (if we're on latest)
        if (result != null)
            await Assert.That(result.Version).IsNotEqualTo(currentVersion);
    }

    [Test]
    [Category("Integration")]
    [Category("External")]
    public async Task CheckForUpdatesAsync_WithoutPrerelease_SkipsPreReleases()
    {
        // Arrange
        var service = CreateService();
        const string currentVersion = "0.1.0-alpha.1";

        // Act
        var result = await service.CheckForUpdatesAsync(currentVersion, false);

        // Assert - If a result is found, it should not be a prerelease
        if (result != null)
        {
            if (result.IsPrerelease)
                throw new InvalidOperationException($"Expected stable release but got prerelease: {result.Version}");

            await Assert.That(result.IsPrerelease).IsFalse();
        }
    }

    [Test]
    [Category("Integration")]
    [Category("External")]
    public async Task CheckForUpdatesAsync_WithStableVersion_FindsNewerStable()
    {
        // Arrange
        var service = CreateService();
        const string currentVersion = "0.0.1"; // Very old version that should find newer

        // Act
        var result = await service.CheckForUpdatesAsync(currentVersion, false);

        // Assert - May be null if no stable releases exist yet
        // This test will pass once a stable release is published
        if (result != null)
        {
            await Assert.That(result.Version).IsNotEqualTo(currentVersion);
            await Assert.That(result.IsPrerelease).IsFalse();
        }
        else
        {
            // No stable releases available yet - test passes
            await Assert.That(result).IsNull();
        }
    }

    [Test]
    [Category("Integration")]
    [Category("External")]
    public async Task CheckForUpdatesAsync_WithLatestVersion_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        // Use a future version that definitely doesn't exist yet
        const string currentVersion = "99.99.99";

        // Act
        var result = await service.CheckForUpdatesAsync(currentVersion, true);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    [Category("Integration")]
    [Category("External")]
    public async Task CheckForUpdatesAsync_IncludesReleaseMetadata()
    {
        // Arrange
        var service = CreateService();
        const string currentVersion = "0.0.1";

        // Act
        var result = await service.CheckForUpdatesAsync(currentVersion, false);

        // Assert - Verify all metadata is populated
        if (result != null)
        {
            await Assert.That(result.TagName).IsNotNull();
            await Assert.That(result.ReleaseUrl).Contains("github.com");
            await Assert.That(result.PublishedAt).IsLessThan(DateTimeOffset.UtcNow);
            await Assert.That(result.Version).IsNotEmpty();
        }
    }
}
