using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class DevicesOptionsViewModelTests
{
    [Test]
    public async Task LoadAsync_NetworkingOnAndConnectedToAPublicNetwork_WarnsThatPeersCannotConnect()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        A.CallTo(() => _networkProfiles.GetPublicNetworkNames()).Returns(["Coffee Shop"]);

        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.HasPublicNetworkWarning).IsTrue();
        await Assert.That(_viewModel.PublicNetworkWarning).Contains("\"Coffee Shop\"");
        await Assert.That(_viewModel.PublicNetworkWarning).Contains("Public");
        await Assert.That(_viewModel.PublicNetworkWarning).Contains("Private");
    }

    [Test]
    public async Task LoadAsync_NoPublicNetwork_ShowsNoWarning()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        A.CallTo(() => _networkProfiles.GetPublicNetworkNames()).Returns([]);

        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.HasPublicNetworkWarning).IsFalse();
        await Assert.That(_viewModel.PublicNetworkWarning).IsNull();
    }

    [Test]
    public async Task LoadAsync_NetworkingOff_ShowsNoWarningOnAPublicNetwork()
    {
        // Arrange
        _configuration.Networking.Enabled = false;
        A.CallTo(() => _networkProfiles.GetPublicNetworkNames()).Returns(["Coffee Shop"]);

        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.HasPublicNetworkWarning).IsFalse();
    }

    [Test]
    public async Task EnableNetworking_TurnedOnWhileOnAPublicNetwork_ShowsTheWarning()
    {
        // Arrange
        _configuration.Networking.Enabled = false;
        A.CallTo(() => _networkProfiles.GetPublicNetworkNames()).Returns(["Coffee Shop"]);
        await _viewModel.LoadAsync();

        // Act
        _viewModel.EnableNetworking = true;

        // Assert
        await Assert.That(_viewModel.HasPublicNetworkWarning).IsTrue();
    }

    [Test]
    public async Task StartWatching_NetworkSwitchedToPrivate_ClearsTheWarningOnRefresh()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        A.CallTo(() => _networkProfiles.GetPublicNetworkNames()).Returns(["Coffee Shop"]);
        await _viewModel.LoadAsync();

        // Act
        A.CallTo(() => _networkProfiles.GetPublicNetworkNames()).Returns([]);
        _viewModel.RefreshNetworkWarning();

        // Assert
        await Assert.That(_viewModel.HasPublicNetworkWarning).IsFalse();
    }

    [Test]
    public async Task LoadAsync_SeveralPublicNetworks_NamesEachOfThem()
    {
        // Arrange
        _configuration.Networking.Enabled = true;
        A.CallTo(() => _networkProfiles.GetPublicNetworkNames()).Returns(["Hotel", "Hotel Wired"]);

        // Act
        await _viewModel.LoadAsync();

        // Assert
        await Assert.That(_viewModel.PublicNetworkWarning).Contains("\"Hotel\"");
        await Assert.That(_viewModel.PublicNetworkWarning).Contains("\"Hotel Wired\"");
    }
}
