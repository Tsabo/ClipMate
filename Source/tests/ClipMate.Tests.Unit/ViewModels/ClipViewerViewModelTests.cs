using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using ClipMate.Tests.Unit.Fakes;
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
        var databases = new FakeDatabases();
        var remote = databases.AddRemote(new RemoteDatabase(new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([3]), Guid.NewGuid()), "Notes", "DESKTOP",
            PeerConnectionStatus.Online, null));
        var key = remote.Key;
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Remote note", CapturedAt = DateTimeOffset.Now };
        remote.WithContent(new ClipContent(clip, [], [], [], [], []));
        var viewModel = new ClipViewerViewModel(databaseManager, databases, NullLogger<ClipViewerViewModel>.Instance);

        // Act
        await viewModel.LoadClipCommand.ExecuteAsync((clip.Id, key));

        // Assert
        await Assert.That(viewModel.CurrentClip).IsSameReferenceAs(clip);
        A.CallTo(databaseManager).MustNotHaveHappened();
    }
}
