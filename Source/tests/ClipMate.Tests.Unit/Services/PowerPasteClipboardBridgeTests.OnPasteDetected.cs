using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteClipboardBridgeTests
{
    [Test]
    [Category("OnPasteDetected")]
    public async Task OnPasteDetected_AfterDelayElapses_AdvancesSequenceOnce()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act
        bridge.OnPasteDetected();
        await Task.Delay(500); // PowerPasteDelay is configured to 200ms in test setup

        // Assert
        _mockPowerPasteService.Verify(p => p.AdvanceToNextAsync(), Times.Once);
    }

    [Test]
    [Category("OnPasteDetected")]
    public async Task OnPasteDetected_CalledRepeatedlyWithinDelayWindow_AdvancesOnlyOnce()
    {
        // Arrange - simulates an application probing several clipboard formats for a single paste.
        // Gaps must stay well under PowerPasteDelay (200ms) with enough margin to survive CI timer
        // jitter (Task.Delay can overshoot by more than Windows' ~15ms timer resolution under load).
        var bridge = CreateBridge();

        // Act
        bridge.OnPasteDetected();
        await Task.Delay(30);
        bridge.OnPasteDetected();
        await Task.Delay(30);
        bridge.OnPasteDetected();
        await Task.Delay(500);

        // Assert
        _mockPowerPasteService.Verify(p => p.AdvanceToNextAsync(), Times.Once);
    }

    [Test]
    [Category("OnPasteDetected")]
    public async Task OnPasteDetected_CalledAgainAfterPreviousAdvanceCompletes_AdvancesTwice()
    {
        // Arrange - two distinct, well-separated pastes should each advance the sequence
        var bridge = CreateBridge();

        // Act
        bridge.OnPasteDetected();
        await Task.Delay(500);

        bridge.OnPasteDetected();
        await Task.Delay(500);

        // Assert
        _mockPowerPasteService.Verify(p => p.AdvanceToNextAsync(), Times.Exactly(2));
    }
}
