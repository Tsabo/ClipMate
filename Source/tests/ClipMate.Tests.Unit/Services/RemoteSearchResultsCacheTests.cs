using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;

namespace ClipMate.Tests.Unit.Services;

public class RemoteSearchResultsCacheTests
{
    private static readonly RemoteDatabaseAddress _work = new(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
    private static readonly RemoteDatabaseAddress _home = new(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());

    [Test]
    public async Task SetResults_KeepsTheLatestResultsPerRemoteDatabase()
    {
        // Arrange
        var cache = new RemoteSearchResultsCache();
        var first = new RemoteSearchResult("report", [new Clip { Id = Guid.NewGuid() }], 1);
        var latest = new RemoteSearchResult("memo", [new Clip { Id = Guid.NewGuid() }], 4212);

        // Act
        cache.SetResults(_work, first);
        cache.SetResults(_work, latest);

        // Assert
        await Assert.That(cache.GetResults(_work)).IsSameReferenceAs(latest);
        await Assert.That(cache.GetResults(_home)).IsNull();
    }

    [Test]
    public async Task ClearResults_ForgetsOnlyThatRemoteDatabase()
    {
        // Arrange
        var cache = new RemoteSearchResultsCache();
        cache.SetResults(_work, new RemoteSearchResult("report", [], 0));
        cache.SetResults(_home, new RemoteSearchResult("memo", [], 0));

        // Act
        cache.ClearResults(_work);

        // Assert
        await Assert.That(cache.GetResults(_work)).IsNull();
        await Assert.That(cache.GetResults(_home)).IsNotNull();
    }

    [Test]
    [Arguments(1000, 4212, "\"report\" (showing 1,000 of 4,212)")]
    [Arguments(3, 3, "\"report\" (3 results)")]
    public async Task RemoteSearchResult_Describe_ShowsHowManyOfTheMatchesArrived(int returned, int total, string expected)
    {
        // Arrange
        var clips = Enumerable.Range(0, returned).Select(_ => new Clip { Id = Guid.NewGuid() }).ToList();

        // Act
        var result = new RemoteSearchResult("report", clips, total);

        // Assert
        await Assert.That(result.Describe()).IsEqualTo(expected);
        await Assert.That(result.IsTruncated).IsEqualTo(total > returned);
    }
}
