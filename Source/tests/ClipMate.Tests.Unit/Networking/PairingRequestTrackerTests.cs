using System.Net;
using ClipMate.Core.Models.Networking;
using ClipMate.Networking;

namespace ClipMate.Tests.Unit.Networking;

public class PairingRequestTrackerTests
{
    private static readonly DeviceId _laptop = DeviceId.FromCertificateBytes([1]);
    private static readonly DeviceId _desktop = DeviceId.FromCertificateBytes([2]);
    private static readonly DeviceId _tablet = DeviceId.FromCertificateBytes([3]);
    private static readonly DeviceId _phone = DeviceId.FromCertificateBytes([4]);
    private static readonly IPAddress _laptopAddress = IPAddress.Parse("192.168.1.10");
    private static readonly IPAddress _desktopAddress = IPAddress.Parse("192.168.1.20");
    private static readonly IPAddress _tabletAddress = IPAddress.Parse("192.168.1.30");
    private static readonly IPAddress _phoneAddress = IPAddress.Parse("192.168.1.40");

    private ManualTimeProvider _clock = null!;
    private PairingRequestTracker _tracker = null!;

    [Before(Test)]
    public void Setup()
    {
        _clock = new ManualTimeProvider();
        _tracker = new PairingRequestTracker(_clock);
    }

    [Test]
    public async Task TryBeginWaiting_AnotherRequestFromTheSameAddressIsWaiting_IsRefused()
    {
        // Arrange: a Device that mints a fresh certificate (a new Device ID) for every request
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);

        // Act
        var began = _tracker.TryBeginWaiting(_desktop, _laptopAddress);

        // Assert
        await Assert.That(began).IsFalse();
    }

    [Test]
    public async Task TryBeginWaiting_ADeviceAtAnotherAddress_IsNotLockedOutByOneAddress()
    {
        // Arrange
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        _tracker.TryBeginWaiting(_desktop, _laptopAddress);
        _tracker.TryBeginWaiting(_tablet, _laptopAddress);

        // Act
        var began = _tracker.TryBeginWaiting(_phone, _phoneAddress);

        // Assert
        await Assert.That(began).IsTrue();
    }

    [Test]
    public async Task TryBeginWaiting_TooManyRequestsWaitingFromDifferentAddresses_IsRefused()
    {
        // Arrange
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        _tracker.TryBeginWaiting(_desktop, _desktopAddress);
        _tracker.TryBeginWaiting(_tablet, _tabletAddress);

        // Act
        var began = _tracker.TryBeginWaiting(_phone, _phoneAddress);

        // Assert
        await Assert.That(began).IsFalse();
    }

    [Test]
    public async Task TryBeginWaiting_SoonAfterADeclinedRequestFromTheSameAddress_IsRefused()
    {
        // Arrange
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        _tracker.EndWaiting(_laptop, false);
        _clock.Advance(TimeSpan.FromSeconds(5));

        // Act
        var sameDevice = _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        var freshDeviceId = _tracker.TryBeginWaiting(_desktop, _laptopAddress);

        // Assert
        await Assert.That(sameDevice).IsFalse();
        await Assert.That(freshDeviceId).IsFalse();
    }

    [Test]
    public async Task TryBeginWaiting_AfterTheCoolDown_IsAllowedAgain()
    {
        // Arrange
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        _tracker.EndWaiting(_laptop, false);
        _clock.Advance(PairingRequestTracker.DeclinedCoolDown + TimeSpan.FromSeconds(1));

        // Act
        var began = _tracker.TryBeginWaiting(_laptop, _laptopAddress);

        // Assert
        await Assert.That(began).IsTrue();
    }

    [Test]
    public async Task TryBeginWaiting_AfterAnAcceptedRequestFromTheSameAddress_IsAllowed()
    {
        // Arrange
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        _tracker.Accept(_laptop, "LAPTOP", null, null);
        _tracker.EndWaiting(_laptop, true);

        // Act
        var began = _tracker.TryBeginWaiting(_desktop, _laptopAddress);

        // Assert
        await Assert.That(began).IsTrue();
    }

    [Test]
    public async Task TryBeginWaiting_ADeclinedRequestFromAnotherAddress_DoesNotDelayThisOne()
    {
        // Arrange
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        _tracker.EndWaiting(_laptop, false);

        // Act
        var began = _tracker.TryBeginWaiting(_desktop, _desktopAddress);

        // Assert
        await Assert.That(began).IsTrue();
    }

    [Test]
    public async Task TryBeginWaiting_IPv4MappedAddress_CountsAsTheSameAddress()
    {
        // Arrange
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);

        // Act
        var began = _tracker.TryBeginWaiting(_desktop, _laptopAddress.MapToIPv6());

        // Assert
        await Assert.That(began).IsFalse();
    }

    [Test]
    public async Task TryBeginWaiting_DuplicateRequestWhileTheFirstIsAccepted_KeepsTheAcceptance()
    {
        // Arrange: the user accepted, and the same Device asks again before the first request has finished
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        _tracker.Accept(_laptop, "LAPTOP", null, null);

        // Act
        var began = _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        _tracker.EndWaiting(_laptop, true);

        // Assert
        await Assert.That(began).IsFalse();
        await Assert.That(_tracker.TryTakeAcceptance(_laptop, out _)).IsTrue();
    }

    [Test]
    public async Task TryBeginWaiting_ANewRequestThatBegins_ReplacesAnEarlierAcceptance()
    {
        // Arrange
        _tracker.TryBeginWaiting(_laptop, _laptopAddress);
        _tracker.Accept(_laptop, "LAPTOP", null, null);
        _tracker.EndWaiting(_laptop, true);

        // Act
        var began = _tracker.TryBeginWaiting(_laptop, _laptopAddress);

        // Assert
        await Assert.That(began).IsTrue();
        await Assert.That(_tracker.TryTakeAcceptance(_laptop, out _)).IsFalse();
    }
}
