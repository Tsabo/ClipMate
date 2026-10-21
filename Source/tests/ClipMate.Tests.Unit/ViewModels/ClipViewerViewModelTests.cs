using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.ViewModels;

public class ClipViewerViewModelTests
{
    [Test]
    public async Task LoadClipCommand_RemoteClip_ReadsItFromThePeerWithoutOpeningALocalDatabase()
    {
        // Arrange
        var databaseManager = A.Fake<IDatabaseManager>();
        var contentService = A.Fake<IClipContentService>();
        var key = RemoteDatabaseKey.From(new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([3]), Guid.NewGuid()));
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Remote note", CapturedAt = DateTimeOffset.Now };
        A.CallTo(() => contentService.GetClipContentAsync(key, clip.Id, A<CancellationToken>._)).Returns(new ClipContent(clip, [], [], [], [], []));
        var viewModel = new ClipViewerViewModel(databaseManager, contentService, NullLogger<ClipViewerViewModel>.Instance);

        // Act
        await viewModel.LoadClipCommand.ExecuteAsync((clip.Id, key));

        // Assert
        await Assert.That(viewModel.CurrentClip).IsSameReferenceAs(clip);
        A.CallTo(databaseManager).MustNotHaveHappened();
    }
}
