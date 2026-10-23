using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using ClipMate.Data;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The clip list showing Remote Search results.
/// </summary>
public class ClipListViewModelRemoteSearchTests
{
    private static readonly RemoteDatabaseAddress _work = new(DeviceId.FromCertificateBytes([1]), Guid.NewGuid());
    private readonly ICollectionService _collectionService = A.Fake<ICollectionService>();
    private readonly IFolderService _folderService = A.Fake<IFolderService>();
    private readonly IRemoteDatabaseService _remoteDatabases = A.Fake<IRemoteDatabaseService>();

    private ClipListViewModel CreateViewModel() => new(_collectionService, _folderService, A.Fake<IClipService>(), A.Fake<IQuickPasteService>(),
        new PowerPasteStatus(), A.Fake<IDatabaseContextFactory>(), _remoteDatabases, A.Fake<IMessenger>(), A.Fake<ILogger<ClipListViewModel>>());

    [Test]
    public async Task Receive_RemoteSearchResultsSelected_ShowsTheCachedMatchesReadOnlyAndHowManyOfTheTotal()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.CurrentDatabaseKey = "db_local";
        viewModel.Clips.Add(new Clip { Id = Guid.NewGuid() });
        var clips = Enumerable.Range(0, 1000).Select(_ => new Clip { Id = Guid.NewGuid() }).ToList();

        // Act
        viewModel.Receive(new RemoteSearchResultsSelectedEvent(_work, new RemoteSearchResult("report", clips, 4212)));

        // Assert
        await Assert.That(viewModel.Clips.Select(p => p.Id)).IsEquivalentTo(clips.Select(p => p.Id));
        await Assert.That(viewModel.IsRemoteDatabase).IsTrue();
        await Assert.That(viewModel.AcceptsNewClips).IsFalse();
        await Assert.That(viewModel.CurrentDatabaseKey).IsEqualTo(RemoteDatabaseKey.From(_work));
        await Assert.That(viewModel.RemoteStatusMessage).IsEqualTo("Showing 1,000 of 4,212 matches");
        A.CallTo(() => _collectionService.SetActiveAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(_remoteDatabases).Where(p => p.Method.Name.EndsWith("Async", StringComparison.Ordinal)).MustNotHaveHappened();
    }

    [Test]
    public async Task Receive_RemoteSearchResultsSelected_AllMatchesArrived_ShowsNoStatus()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var clips = new List<Clip> { new() { Id = Guid.NewGuid() } };

        // Act
        viewModel.Receive(new RemoteSearchResultsSelectedEvent(_work, new RemoteSearchResult("report", clips, 1)));

        // Assert
        await Assert.That(viewModel.Clips.Count).IsEqualTo(1);
        await Assert.That(viewModel.RemoteStatusMessage).IsNull();
    }
}
