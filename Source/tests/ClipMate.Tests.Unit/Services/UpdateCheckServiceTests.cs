using System.Net;
using ClipMate.Core.ValueObjects;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class UpdateCheckServiceTests
{
    [Test]
    public async Task ParseVersionFromTag_RemovesVPrefix()
    {
        var result = ApplicationVersion.ParseVersionFromTag("v1.2.3");

        await Assert.That(result).IsEqualTo("1.2.3");
    }

    [Test]
    public async Task ParseVersionFromTag_HandlesNoPrefix()
    {
        var result = ApplicationVersion.ParseVersionFromTag("1.2.3");

        await Assert.That(result).IsEqualTo("1.2.3");
    }

    [Test]
    public async Task ParseVersionFromTag_HandlesEmptyString()
    {
        var result = ApplicationVersion.ParseVersionFromTag("");

        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task IsNewerThan_ReturnsTrueForNewerVersion()
    {
        var version = new ApplicationVersion(
            "v2.0.0",
            "2.0.0",
            "https://github.com/clipmate/ClipMate/releases/tag/v2.0.0",
            DateTimeOffset.UtcNow,
            false);

        var result = version.IsNewerThan("1.0.0");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsNewerThan_ReturnsFalseForOlderVersion()
    {
        var version = new ApplicationVersion(
            "v1.0.0",
            "1.0.0",
            "https://github.com/clipmate/ClipMate/releases/tag/v1.0.0",
            DateTimeOffset.UtcNow,
            false);

        var result = version.IsNewerThan("2.0.0");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsNewerThan_ReturnsFalseForSameVersion()
    {
        var version = new ApplicationVersion(
            "v1.0.0",
            "1.0.0",
            "https://github.com/clipmate/ClipMate/releases/tag/v1.0.0",
            DateTimeOffset.UtcNow,
            false);

        var result = version.IsNewerThan("1.0.0");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsNewerThan_HandlesMinorVersionDifference()
    {
        var version = new ApplicationVersion(
            "v1.1.0",
            "1.1.0",
            "https://github.com/clipmate/ClipMate/releases/tag/v1.1.0",
            DateTimeOffset.UtcNow,
            false);

        var result = version.IsNewerThan("1.0.0");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsNewerThan_HandlesPatchVersionDifference()
    {
        var version = new ApplicationVersion(
            "v1.0.1",
            "1.0.1",
            "https://github.com/clipmate/ClipMate/releases/tag/v1.0.1",
            DateTimeOffset.UtcNow,
            false);

        var result = version.IsNewerThan("1.0.0");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsNewerThan_ReturnsFalseForInvalidVersionString()
    {
        var version = new ApplicationVersion(
            "v1.0.0",
            "1.0.0",
            "https://github.com/clipmate/ClipMate/releases/tag/v1.0.0",
            DateTimeOffset.UtcNow,
            false);

        var result = version.IsNewerThan("invalid");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsNewerThan_ReturnsFalseWhenThisVersionIsInvalid()
    {
        var version = new ApplicationVersion(
            "vinvalid",
            "invalid",
            "https://github.com/clipmate/ClipMate/releases/tag/vinvalid",
            DateTimeOffset.UtcNow,
            false);

        var result = version.IsNewerThan("1.0.0");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task CheckForUpdatesAsync_ReturnsNullWhenNoNewerVersion()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.SetupGitHubReleasesResponse("1.0.0");

        var mockLogger = new Mock<ILogger<UpdateCheckService>>();

        var service = new UpdateCheckService(new HttpClient(mockHttp), mockLogger.Object);

        var result = await service.CheckForUpdatesAsync("2.0.0");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task CheckForUpdatesAsync_ReturnsVersionWhenNewerAvailable()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.SetupGitHubReleasesResponse("2.0.0");

        var mockLogger = new Mock<ILogger<UpdateCheckService>>();

        var service = new UpdateCheckService(new HttpClient(mockHttp), mockLogger.Object);

        var result = await service.CheckForUpdatesAsync("1.0.0");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Version).IsEqualTo("2.0.0");
    }

    [Test]
    public async Task CheckForUpdatesAsync_ExcludesPrereleaseByDefault()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.SetupGitHubReleasesResponseWithPrerelease("2.0.0");

        var mockLogger = new Mock<ILogger<UpdateCheckService>>();

        var service = new UpdateCheckService(new HttpClient(mockHttp), mockLogger.Object);

        var result = await service.CheckForUpdatesAsync("1.0.0");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task CheckForUpdatesAsync_IncludesPrereleaseWhenRequested()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.SetupGitHubReleasesResponseWithPrerelease("2.0.0");

        var mockLogger = new Mock<ILogger<UpdateCheckService>>();

        var service = new UpdateCheckService(new HttpClient(mockHttp), mockLogger.Object);

        var result = await service.CheckForUpdatesAsync("1.0.0", true);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Version).IsEqualTo("2.0.0");
        await Assert.That(result.IsPrerelease).IsTrue();
    }

    [Test]
    public async Task CheckForUpdatesAsync_HandlesHttpErrors()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.SetupHttpError();

        var mockLogger = new Mock<ILogger<UpdateCheckService>>();

        var service = new UpdateCheckService(new HttpClient(mockHttp), mockLogger.Object);

        var result = await service.CheckForUpdatesAsync("1.0.0");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task CheckForUpdatesAsync_HandlesInvalidJson()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.SetupInvalidJson();

        var mockLogger = new Mock<ILogger<UpdateCheckService>>();

        var service = new UpdateCheckService(new HttpClient(mockHttp), mockLogger.Object);

        var result = await service.CheckForUpdatesAsync("1.0.0");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task CheckForUpdatesAsync_SelectsLatestRelease()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.SetupMultipleReleases(["2.0.0", "1.5.0", "1.0.0"]);

        var mockLogger = new Mock<ILogger<UpdateCheckService>>();

        var service = new UpdateCheckService(new HttpClient(mockHttp), mockLogger.Object);

        var result = await service.CheckForUpdatesAsync("1.0.0");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Version).IsEqualTo("2.0.0");
    }
}

/// <summary>
/// Mock HttpMessageHandler for testing UpdateCheckService without real HTTP calls.
/// </summary>
file class MockHttpMessageHandler : HttpMessageHandler
{
    private string? _responseContent;
    private bool _shouldFail;

    public void SetupGitHubReleasesResponse(string version)
    {
        _responseContent = $$"""
                             [
                               {
                                 "tag_name": "v{{version}}",
                                 "html_url": "https://github.com/clipmate/ClipMate/releases/tag/v{{version}}",
                                 "published_at": "2026-01-03T00:00:00Z",
                                 "prerelease": false
                               }
                             ]
                             """;
    }

    public void SetupGitHubReleasesResponseWithPrerelease(string version)
    {
        _responseContent = $$"""
                             [
                               {
                                 "tag_name": "v{{version}}",
                                 "html_url": "https://github.com/clipmate/ClipMate/releases/tag/v{{version}}",
                                 "published_at": "2026-01-03T00:00:00Z",
                                 "prerelease": true
                               }
                             ]
                             """;
    }

    public void SetupMultipleReleases(string[] versions)
    {
        var releases = versions.Select((v, i) => $$"""
                                                     {
                                                       "tag_name": "v{{v}}",
                                                       "html_url": "https://github.com/clipmate/ClipMate/releases/tag/v{{v}}",
                                                       "published_at": "2026-01-0{{3 - i}}T00:00:00Z",
                                                       "prerelease": false
                                                     }
                                                   """);

        _responseContent = $"[\n{string.Join(",\n", releases)}\n]";
    }

    public void SetupHttpError()
    {
        _shouldFail = true;
    }

    public void SetupInvalidJson()
    {
        _responseContent = "invalid json {";
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_shouldFail)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseContent ?? "[]"),
        };

        return Task.FromResult(response);
    }
}
