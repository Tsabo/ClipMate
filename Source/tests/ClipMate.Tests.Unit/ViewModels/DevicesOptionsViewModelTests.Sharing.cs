using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class DevicesOptionsViewModelTests
{
    [Test]
    public async Task LoadAsync_NoDatabaseShared_SaysNothingIsSharedAndHowToShare()
    {
        // Arrange
        _configuration.Databases["db_1"] = new DatabaseConfiguration { Name = "My Clips", ShareWithPeers = false };

        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.SharesNothing).IsTrue();
        await Assert.That(_viewModel.SharedDatabasesText).Contains("Nothing shared yet");
        await Assert.That(_viewModel.SharedDatabasesText).Contains("Share with Peers");
    }

    [Test]
    public async Task LoadAsync_DatabasesShared_NamesEachSharedDatabase()
    {
        // Arrange
        _configuration.Databases["db_1"] = new DatabaseConfiguration { Name = "My Clips", ShareWithPeers = true };
        _configuration.Databases["db_2"] = new DatabaseConfiguration { Name = "Private", ShareWithPeers = false };
        _configuration.Databases["db_3"] = new DatabaseConfiguration { Name = "Work", ShareWithPeers = true };

        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.SharesNothing).IsFalse();
        await Assert.That(_viewModel.SharedDatabasesText).Contains("My Clips");
        await Assert.That(_viewModel.SharedDatabasesText).Contains("Work");
        await Assert.That(_viewModel.SharedDatabasesText).DoesNotContain("Private");
    }

    [Test]
    public async Task StartWatching_PeerSharingDatabases_ListsThemUsingRemoteDatabaseAliases()
    {
        // Arrange
        var desktop = DeviceId.FromCertificateBytes([2]);
        var clips = Guid.NewGuid();
        var work = Guid.NewGuid();
        A.CallTo(() => _peerStore.GetPeers()).Returns(
        [
            new Peer(desktop, "DESKTOP", null, [], DateTime.Now)
            {
                SharedDatabases = [new SharedDatabase(clips, "My Clips"), new SharedDatabase(work, "Work")],
                DatabaseAliases = new Dictionary<Guid, string> { [work] = "Office clips" },
            },
        ]);

        // Act
        _viewModel.StartWatching();

        // Assert
        await Assert.That(_viewModel.Peers.Single().SharedDatabasesText).IsEqualTo("My Clips, Office clips");
    }

    [Test]
    public async Task StartWatching_PeerReachedButSharingNothing_SaysSo()
    {
        // Arrange
        var desktop = DeviceId.FromCertificateBytes([2]);
        A.CallTo(() => _peerStore.GetPeers()).Returns([new Peer(desktop, "DESKTOP", null, [], DateTime.Now)]);

        // Act
        _viewModel.StartWatching();

        // Assert
        await Assert.That(_viewModel.Peers.Single().SharedDatabasesText).IsEqualTo("Nothing shared");
    }

    [Test]
    public async Task StartWatching_PeerNeverReached_SaysItsSharesAreNotKnownYet()
    {
        // Arrange
        var desktop = DeviceId.FromCertificateBytes([2]);
        A.CallTo(() => _peerStore.GetPeers()).Returns([new Peer(desktop, "DESKTOP", null, [], null)]);

        // Act
        _viewModel.StartWatching();

        // Assert
        await Assert.That(_viewModel.Peers.Single().SharedDatabasesText).IsEqualTo("Not known yet");
    }

    [Test]
    public async Task ConfigurationSaved_WhileWatching_RefreshesThisDevicesSharedDatabases()
    {
        // Arrange
        _configuration.Databases["db_1"] = new DatabaseConfiguration { Name = "My Clips", ShareWithPeers = false };
        await _viewModel.LoadAsync();
        _viewModel.StartWatching();

        // Act
        _configuration.Databases["db_1"].ShareWithPeers = true;
        _configurationService.Saved += Raise.WithEmpty();

        // Assert
        await Assert.That(_viewModel.SharesNothing).IsFalse();
        await Assert.That(_viewModel.SharedDatabasesText).Contains("My Clips");
    }
}
