using ClipMate.Platform.Services;

namespace ClipMate.Tests.Unit.Platform;

public class WindowsNetworkProfileServiceTests
{
    private const int _public = 0;
    private const int _private = 1;
    private const int _domain = 2;
    private const int _noTraffic = 0x1 | 0x2;
    private const int _ipv4LocalNetwork = 0x20;
    private const int _ipv4Internet = 0x40;
    private const int _ipv6Subnet = 0x100;

    [Test]
    public async Task SelectPublicNetworkNames_PublicNetworkCarryingTraffic_IsReported()
    {
        // Act
        var names = WindowsNetworkProfileService.SelectPublicNetworkNames([new ConnectedNetwork("Coffee Shop", _public, _ipv4Internet)]);

        // Assert
        await Assert.That(names).IsEquivalentTo(["Coffee Shop"]);
    }

    [Test]
    public async Task SelectPublicNetworkNames_PublicNetworkWithLocalOrSubnetTrafficOnly_IsReported()
    {
        // Act
        var names = WindowsNetworkProfileService.SelectPublicNetworkNames(
        [
            new ConnectedNetwork("Office LAN", _public, _ipv4LocalNetwork),
            new ConnectedNetwork("Lab", _public, _ipv6Subnet),
        ]);

        // Assert
        await Assert.That(names).IsEquivalentTo(["Office LAN", "Lab"]);
    }

    [Test]
    public async Task SelectPublicNetworkNames_PublicNetworkWithNoTraffic_IsIgnored()
    {
        // Arrange: e.g. a Hyper-V virtual switch, which Windows lists as a Public "Unidentified network"

        // Act
        var names = WindowsNetworkProfileService.SelectPublicNetworkNames([new ConnectedNetwork("Unidentified network", _public, _noTraffic)]);

        // Assert
        await Assert.That(names).IsEmpty();
    }

    [Test]
    public async Task SelectPublicNetworkNames_PrivateAndDomainNetworks_AreIgnored()
    {
        // Act
        var names = WindowsNetworkProfileService.SelectPublicNetworkNames(
        [
            new ConnectedNetwork("Home", _private, _ipv4Internet),
            new ConnectedNetwork("Corp", _domain, _ipv4Internet),
        ]);

        // Assert
        await Assert.That(names).IsEmpty();
    }

    [Test]
    public async Task SelectPublicNetworkNames_SameNameTwice_IsReportedOnce()
    {
        // Act
        var names = WindowsNetworkProfileService.SelectPublicNetworkNames(
        [
            new ConnectedNetwork("Hotel", _public, _ipv4Internet),
            new ConnectedNetwork("Hotel", _public, _ipv4LocalNetwork),
        ]);

        // Assert
        await Assert.That(names).IsEquivalentTo(["Hotel"]);
    }

    [Test]
    public async Task ReadConnectedNetworks_OnThisMachine_ReadsTheNetworkListManager()
    {
        // Act: the real COM call, which GetPublicNetworkNames would otherwise hide behind an empty list if it failed
        var networks = WindowsNetworkProfileService.ReadConnectedNetworks();

        // Assert
        await Assert.That(networks).IsNotNull();
        await Assert.That(networks.All(p => p.Category is _public or _private or _domain)).IsTrue();
    }
}
