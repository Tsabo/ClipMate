using ClipMate.Core.Models.Networking;
using ClipMate.Networking;

namespace ClipMate.Tests.Unit.Networking;

public class PairingRequestTrackerTests
{
    private static readonly DeviceId _laptop = DeviceId.FromCertificateBytes([1]);

    private ManualTimeProvider _clock = null!;
    private PairingRequestTracker _tracker = null!;

    [Before(Test)]
    public void Setup()
    {
        _clock = new ManualTimeProvider();
        _tracker = new PairingRequestTracker(_clock);
    }

    [Test]
    public async Task TryBeginWaiting_DuplicateRequestWhileTheFirstIsAccepted_KeepsTheAcceptance()
    {
        // Arrange: the user accepted, and the same Device asks again before the first request has finished
        _tracker.TryBeginWaiting(_laptop);
        _tracker.Accept(_laptop, "LAPTOP", null, null);

        // Act
        var began = _tracker.TryBeginWaiting(_laptop);
        _tracker.EndWaiting(_laptop);

        // Assert
        await Assert.That(began).IsFalse();
        await Assert.That(_tracker.TryTakeAcceptance(_laptop, out _)).IsTrue();
    }

    [Test]
    public async Task TryBeginWaiting_ANewRequestThatBegins_ReplacesAnEarlierAcceptance()
    {
        // Arrange
        _tracker.TryBeginWaiting(_laptop);
        _tracker.Accept(_laptop, "LAPTOP", null, null);
        _tracker.EndWaiting(_laptop);

        // Act
        var began = _tracker.TryBeginWaiting(_laptop);

        // Assert
        await Assert.That(began).IsTrue();
        await Assert.That(_tracker.TryTakeAcceptance(_laptop, out _)).IsFalse();
    }
}
