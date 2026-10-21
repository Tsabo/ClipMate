using ClipMate.Core.Models.Networking;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class DevicesOptionsViewModelTests
{
    [Test]
    public async Task AddDeviceManuallyCommand_OpensAddDeviceManually()
    {
        // Act
        await _viewModel.AddDeviceManuallyCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => _pairingDialogs.AddDeviceManuallyAsync()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task AddDeviceManuallyCommand_WhenPairingCompletes_ListsTheNewPeer()
    {
        // Arrange
        IReadOnlyList<Peer> peers = [];
        A.CallTo(() => _peerStore.GetPeers()).ReturnsLazily(() => peers);
        A.CallTo(() => _pairingDialogs.AddDeviceManuallyAsync())
            .Invokes(() => peers = [new Peer(DeviceId.FromCertificateBytes([7]), "VPN-DESKTOP", null, ["vpn-host:47823"], null)]);

        // Act
        await _viewModel.AddDeviceManuallyCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.Peers.Select(p => p.DisplayName)).IsEquivalentTo(["VPN-DESKTOP"]);
    }
}
