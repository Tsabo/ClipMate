using ClipMate.Core.Models.Networking;
using ClipMate.Networking;
using ClipMate.Networking.Protocol;
using Grpc.Core;

namespace ClipMate.Tests.Integration.Networking;

public partial class PairingTests
{
    [Test]
    public async Task Unpair_WhenThePeerIsReachable_RemovesThePeerOnBothDevices()
    {
        // Arrange
        await _laptop.PairWithAsync(_desktop);
        var desktopUnpaired = WaitForUnpairing(_desktop, _laptop.DeviceId);

        // Act
        var told = await _laptop.Unpairing.UnpairAsync(_desktop.DeviceId);

        // Assert
        await Assert.That(told).IsTrue();
        await Assert.That(_laptop.Store.IsPaired(_desktop.DeviceId)).IsFalse();
        await desktopUnpaired.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(_desktop.Store.IsPaired(_laptop.DeviceId)).IsFalse();
    }

    [Test]
    public async Task Unpair_NeitherDeviceCanConnectAfterwards()
    {
        // Arrange
        await _laptop.PairWithAsync(_desktop);
        var desktopUnpaired = WaitForUnpairing(_desktop, _laptop.DeviceId);

        // Act
        await _laptop.Unpairing.UnpairAsync(_desktop.DeviceId);
        await desktopUnpaired.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        await Assert.That(async () => await _laptop.HelloAsync(_desktop)).Throws<RpcException>();
        await Assert.That(async () => await _desktop.HelloAsync(_laptop)).Throws<RpcException>();
    }

    [Test]
    public async Task Unpair_DropsTheFormerPeersOpenConnectionsImmediately()
    {
        // Arrange: the desktop has a call open on a Peer connection to the laptop (the laptop's user never answers it).
        await _laptop.PairWithAsync(_desktop);
        _laptop.User.Answer = null;
        using var channel = PeerChannelFactory.CreateForPeer(_laptop.Address, _desktop.Identity, _laptop.DeviceId);
        var client = new PeerService.PeerServiceClient(channel);
        using var openCall = client.RequestPairingAsync(new PairRequest { DeviceName = "DESKTOP", ProtocolMajor = PeerProtocol.Major });
        await _laptop.User.Asked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Act: the laptop unpairs locally only (as if the desktop could not be told).
        await _laptop.Store.RemoveAsync(_desktop.DeviceId);

        // Assert: the open call ends at once instead of waiting for the user.
        var exception = await Assert.That(async () => await openCall.ResponseAsync.WaitAsync(TimeSpan.FromSeconds(10))).Throws<RpcException>();
        await Assert.That(exception!.StatusCode).IsNotEqualTo(StatusCode.DeadlineExceeded);
    }

    [Test]
    public async Task Unpair_WhenThePeerIsOffline_UnpairsHereAndThePeerIsRefusedAndLearnsItLater()
    {
        // Arrange
        await _laptop.PairWithAsync(_desktop);
        _desktop.PretendPairedLongAgo();
        await _desktop.StopHostAsync();

        // Act
        var told = await _laptop.Unpairing.UnpairAsync(_desktop.DeviceId);

        // Assert: unpaired here even though the desktop could not be told.
        await Assert.That(told).IsFalse();
        await Assert.That(_laptop.Store.IsPaired(_desktop.DeviceId)).IsFalse();

        // The desktop comes back: it is refused, and learns it was unpaired the next time it reaches the laptop.
        await _desktop.StartHostAsync();
        await Assert.That(async () => await _desktop.HelloAsync(_laptop)).Throws<RpcException>();
        await _desktop.CheckPeersAsync();
        await Assert.That(_desktop.Store.IsPaired(_laptop.DeviceId)).IsFalse();
    }

    [Test]
    public async Task OneSidedPairing_TheDeviceThatStillHasThePeerLearnsItIsNotPaired()
    {
        // Arrange: the laptop recorded the desktop, but the desktop never completed Pairing (e.g. a lost confirmation).
        await _laptop.Store.SavePairedAsync(_desktop.DeviceId, "DESKTOP", null, _desktop.Address);
        _laptop.PretendPairedLongAgo();

        // Act
        await _laptop.CheckPeersAsync();

        // Assert
        await Assert.That(_laptop.Store.IsPaired(_desktop.DeviceId)).IsFalse();
    }

    [Test]
    public async Task CheckPeers_JustAfterPairing_DoesNotUnpairAPeerThatIsStillCompletingPairing()
    {
        // Arrange: the approving Device records the Peer before the requesting Device does.
        await _laptop.Store.SavePairedAsync(_desktop.DeviceId, "DESKTOP", null, _desktop.Address);

        // Act
        await _laptop.CheckPeersAsync();

        // Assert
        await Assert.That(_laptop.Store.IsPaired(_desktop.DeviceId)).IsTrue();
    }

    [Test]
    public async Task CheckPeers_PeerThatIsStillPaired_StaysPaired()
    {
        // Arrange
        await _laptop.PairWithAsync(_desktop);

        // Act
        await _laptop.CheckPeersAsync();

        // Assert
        await Assert.That(_laptop.Store.IsPaired(_desktop.DeviceId)).IsTrue();
    }

    [Test]
    public async Task CheckPeers_PeerThatIsOffline_StaysPaired()
    {
        // Arrange
        await _laptop.PairWithAsync(_desktop);
        await _desktop.StopHostAsync();

        // Act
        await _laptop.CheckPeersAsync();

        // Assert
        await Assert.That(_laptop.Store.IsPaired(_desktop.DeviceId)).IsTrue();
    }

    [Test]
    public async Task RePairing_AfterUnpairing_NeedsApprovalAgain()
    {
        // Arrange
        await _laptop.PairWithAsync(_desktop);
        await _laptop.Unpairing.UnpairAsync(_desktop.DeviceId);
        _desktop.User.Answer = PairingDecision.Declined;

        // Act
        await using var session = _laptop.PairingClient.Start(_desktop.AsTarget());
        var status = await session.AcceptAsync(null);

        // Assert
        await Assert.That(status).IsEqualTo(PairingStatus.Declined);
        await Assert.That(_desktop.User.Requests).Count().IsEqualTo(2);
        await Assert.That(_laptop.Store.IsPaired(_desktop.DeviceId)).IsFalse();
        await Assert.That(_desktop.Store.IsPaired(_laptop.DeviceId)).IsFalse();
    }

    [Test]
    public async Task PeerAlias_IsNeverSentToThePeer()
    {
        // Arrange
        await _laptop.PairWithAsync(_desktop);

        // Act
        await _laptop.Store.SetAliasAsync(_desktop.DeviceId, "Office desktop");
        await _desktop.CheckPeersAsync();

        // Assert
        await Assert.That(_laptop.Store.Find(_desktop.DeviceId)!.DisplayName).IsEqualTo("Office desktop");
        var laptopOnDesktop = _desktop.Store.Find(_laptop.DeviceId)!;
        await Assert.That(laptopOnDesktop.DisplayName).IsEqualTo("LAPTOP");
        await Assert.That(laptopOnDesktop.DeviceName).IsEqualTo("LAPTOP");
    }

    private static Task WaitForUnpairing(TestDevice device, DeviceId peer)
    {
        var unpaired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        device.Store.PeerRemoved += (_, e) =>
        {
            if (e.DeviceId == peer)
                unpaired.TrySetResult();
        };
        return unpaired.Task;
    }
}
