using System.Net;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class DevicesOptionsViewModelTests
{
    [Test]
    public async Task StartWatching_ListsPeersWithAliasDeviceNameShortDeviceIdAndStatus()
    {
        // Arrange
        var laptop = DeviceId.FromCertificateBytes([1]);
        var desktop = DeviceId.FromCertificateBytes([2]);
        A.CallTo(() => _peerStore.GetPeers()).Returns(
        [
            new Peer(laptop, "LAPTOP-01", "Travel laptop", [], DateTime.Now.AddHours(-2)),
            new Peer(desktop, "DESKTOP", null, [], null),
        ]);
        A.CallTo(() => _peerStatus.GetStatus(laptop)).Returns(PeerConnectionStatus.Offline);
        A.CallTo(() => _peerStatus.GetStatus(desktop)).Returns(PeerConnectionStatus.Online);

        // Act
        _viewModel.StartWatching();

        // Assert
        await Assert.That(_viewModel.HasPeers).IsTrue();
        var laptopItem = _viewModel.Peers.Single(p => p.DeviceName == "LAPTOP-01");
        await Assert.That(laptopItem.DisplayName).IsEqualTo("Travel laptop");
        await Assert.That(laptopItem.ShortDeviceId).IsEqualTo(laptop.ToShortDisplayString());
        await Assert.That(laptopItem.StatusText).IsEqualTo("Offline (last seen 2 h ago)");

        var desktopItem = _viewModel.Peers.Single(p => p.DeviceName == "DESKTOP");
        await Assert.That(desktopItem.DisplayName).IsEqualTo("DESKTOP");
        await Assert.That(desktopItem.StatusText).IsEqualTo("Online");
    }

    [Test]
    public async Task PeerStatusChanged_WhileWatching_UpdatesTheStatus()
    {
        // Arrange
        var laptop = DeviceId.FromCertificateBytes([1]);
        A.CallTo(() => _peerStore.GetPeers()).Returns([new Peer(laptop, "LAPTOP", null, [], null)]);
        A.CallTo(() => _peerStatus.GetStatus(laptop)).Returns(PeerConnectionStatus.Online);
        _viewModel.StartWatching();

        // Act
        A.CallTo(() => _peerStatus.GetStatus(laptop)).Returns(PeerConnectionStatus.UpdateRequired);
        _peerStatus.StatusChanged += Raise.WithEmpty();

        // Assert
        await Assert.That(_viewModel.Peers.Single().StatusText).IsEqualTo("Update required");
    }

    [Test]
    public async Task PeersChanged_WhileWatching_ListsTheNewPeer()
    {
        // Arrange
        _viewModel.StartWatching();

        // Act
        A.CallTo(() => _peerStore.GetPeers()).Returns([new Peer(DeviceId.FromCertificateBytes([1]), "LAPTOP", null, [], null)]);
        _peerStore.PeersChanged += Raise.WithEmpty();

        // Assert
        await Assert.That(_viewModel.Peers.Select(p => p.DisplayName)).IsEquivalentTo(["LAPTOP"]);
    }

    [Test]
    public async Task StartWatching_DiscoveredDeviceThatIsAPeer_IsMarkedPairedAndCannotBePairedAgain()
    {
        // Arrange
        var desktop = CreateDiscoveredDevice("Desktop");
        A.CallTo(() => _discovery.IsRunning).Returns(true);
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([desktop]);
        A.CallTo(() => _peerStore.IsPaired(desktop.DeviceId)).Returns(true);

        // Act
        _viewModel.StartWatching();

        // Assert
        var item = _viewModel.DiscoveredDevices.Single();
        await Assert.That(item.StatusText).IsEqualTo("Paired");
        await Assert.That(item.CanPair).IsFalse();
    }

    [Test]
    public async Task StartWatching_DiscoveredDeviceWithAnotherMajorVersion_ShowsUpdateRequired()
    {
        // Arrange
        var future = CreateDiscoveredDevice("Future") with { ProtocolMajor = ClipMate.Networking.PeerProtocol.Major + 1 };
        A.CallTo(() => _discovery.IsRunning).Returns(true);
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([future]);

        // Act
        _viewModel.StartWatching();

        // Assert
        var item = _viewModel.DiscoveredDevices.Single();
        await Assert.That(item.StatusText).IsEqualTo("Update required");
        await Assert.That(item.CanPair).IsFalse();
    }

    [Test]
    public async Task PairCommand_OpensPairingWithTheDiscoveredDevice()
    {
        // Arrange
        var desktop = CreateDiscoveredDevice("Desktop");
        A.CallTo(() => _discovery.IsRunning).Returns(true);
        A.CallTo(() => _discovery.DiscoveredDevices).Returns([desktop]);
        _viewModel.StartWatching();

        // Act
        await _viewModel.PairCommand.ExecuteAsync(_viewModel.DiscoveredDevices.Single());

        // Assert
        A.CallTo(() => _pairingDialogs.PairAsync(new PairingTarget(desktop.DeviceId, "Desktop", "192.168.1.20:47823")))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RenamePeerCommand_SetsThePeerAliasTheUserEntered()
    {
        // Arrange
        var laptop = new Peer(DeviceId.FromCertificateBytes([1]), "LAPTOP", null, [], null);
        A.CallTo(() => _peerStore.GetPeers()).Returns([laptop]);
        A.CallTo(() => _peerDialogs.AskPeerAlias(laptop)).Returns("Travel laptop");
        _viewModel.StartWatching();

        // Act
        await _viewModel.RenamePeerCommand.ExecuteAsync(_viewModel.Peers.Single());

        // Assert
        A.CallTo(() => _peerStore.SetAliasAsync(laptop.DeviceId, "Travel laptop", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RenamePeerCommand_BlankAlias_ClearsThePeerAlias()
    {
        // Arrange
        var laptop = new Peer(DeviceId.FromCertificateBytes([1]), "LAPTOP", "Travel laptop", [], null);
        A.CallTo(() => _peerStore.GetPeers()).Returns([laptop]);
        A.CallTo(() => _peerDialogs.AskPeerAlias(laptop)).Returns(string.Empty);
        _viewModel.StartWatching();

        // Act
        await _viewModel.RenamePeerCommand.ExecuteAsync(_viewModel.Peers.Single());

        // Assert
        A.CallTo(() => _peerStore.SetAliasAsync(laptop.DeviceId, string.Empty, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RenamePeerCommand_Cancelled_ChangesNothing()
    {
        // Arrange
        var laptop = new Peer(DeviceId.FromCertificateBytes([1]), "LAPTOP", null, [], null);
        A.CallTo(() => _peerStore.GetPeers()).Returns([laptop]);
        A.CallTo(() => _peerDialogs.AskPeerAlias(laptop)).Returns(null);
        _viewModel.StartWatching();

        // Act
        await _viewModel.RenamePeerCommand.ExecuteAsync(_viewModel.Peers.Single());

        // Assert
        A.CallTo(() => _peerStore.SetAliasAsync(A<DeviceId>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task UnpairCommand_Confirmed_UnpairsThePeer()
    {
        // Arrange
        var laptop = new Peer(DeviceId.FromCertificateBytes([1]), "LAPTOP", null, [], null);
        A.CallTo(() => _peerStore.GetPeers()).Returns([laptop]);
        A.CallTo(() => _peerDialogs.ConfirmUnpair(laptop)).Returns(true);
        _viewModel.StartWatching();

        // Act
        await _viewModel.UnpairCommand.ExecuteAsync(_viewModel.Peers.Single());

        // Assert
        A.CallTo(() => _unpairing.UnpairAsync(laptop.DeviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task UnpairCommand_NotConfirmed_KeepsThePeer()
    {
        // Arrange
        var laptop = new Peer(DeviceId.FromCertificateBytes([1]), "LAPTOP", null, [], null);
        A.CallTo(() => _peerStore.GetPeers()).Returns([laptop]);
        A.CallTo(() => _peerDialogs.ConfirmUnpair(laptop)).Returns(false);
        _viewModel.StartWatching();

        // Act
        await _viewModel.UnpairCommand.ExecuteAsync(_viewModel.Peers.Single());

        // Assert
        A.CallTo(() => _unpairing.UnpairAsync(A<DeviceId>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    [Arguments(PeerConnectionStatus.Online, null, "Online")]
    [Arguments(PeerConnectionStatus.UpdateRequired, 5, "Update required")]
    [Arguments(PeerConnectionStatus.Offline, null, "Offline (never seen)")]
    [Arguments(PeerConnectionStatus.Offline, 0, "Offline (last seen just now)")]
    [Arguments(PeerConnectionStatus.Offline, 25, "Offline (last seen 25 min ago)")]
    [Arguments(PeerConnectionStatus.Offline, 180, "Offline (last seen 3 h ago)")]
    [Arguments(PeerConnectionStatus.Unknown, 3000, "Offline (last seen 2 d ago)")]
    public async Task FormatStatus_DescribesTheStatus(PeerConnectionStatus status, int? minutesAgo, string expected)
    {
        // Arrange
        var now = new DateTime(2026, 10, 20, 12, 0, 0);
        DateTime? lastSeen = minutesAgo == null ? null : now.AddMinutes(-minutesAgo.Value);

        // Act
        var text = PeerItem.FormatStatus(status, lastSeen, now);

        // Assert
        await Assert.That(text).IsEqualTo(expected);
    }
}
