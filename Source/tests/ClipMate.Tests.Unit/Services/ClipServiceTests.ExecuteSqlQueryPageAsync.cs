using ClipMate.Core.Models;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class ClipServiceTests
{
    [Test]
    public async Task ExecuteSqlQueryPageAsync_StoredSqlWithDatePlaceholders_PagesTheQueryWithThePlaceholdersReplaced()
    {
        // Arrange
        var page = new ClipPage([], 20, 0);
        A.CallTo(() => MockClipRepository.ExecuteSqlPageAsync(A<string>._, A<int>._, A<int>._, A<CancellationToken>._)).Returns(page);
        var service = CreateClipService();
        var today = DateTime.Now.Date.ToString("yyyy-MM-dd HH:mm:ss");
        var weekAgo = DateTime.Now.Date.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss");

        // Act
        var result = await service.ExecuteSqlQueryPageAsync(TestDatabaseKey,
            "select Clips.* from Clips where CapturedAt >= '#DATEMINUSLIMIT#' and CapturedAt < '#date#'", 7, 20, 10);

        // Assert
        await Assert.That(result).IsSameReferenceAs(page);
        A.CallTo(() => MockClipRepository.ExecuteSqlPageAsync(
                $"select Clips.* from Clips where CapturedAt >= '{weekAgo}' and CapturedAt < '{today}'", 20, 10, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ExecuteSqlQueryPageAsync_EmptySql_Throws()
    {
        // Arrange
        var service = CreateClipService();

        // Act / Assert
        await Assert.That(async () => await service.ExecuteSqlQueryPageAsync(TestDatabaseKey, " ", 7, 0, 10)).Throws<ArgumentException>();
    }
}
