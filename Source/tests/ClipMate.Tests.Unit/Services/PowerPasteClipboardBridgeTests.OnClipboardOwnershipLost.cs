using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteClipboardBridgeTests
{
    [Test]
    [Category("OnClipboardOwnershipLost")]
    public Task OnClipboardOwnershipLost_StopsPowerPaste()
    {
        // Arrange - simulates the user copying something new elsewhere while PowerPaste is active
        var bridge = CreateBridge();

        // Act
        bridge.OnClipboardOwnershipLost();

        // Assert
        _mockPowerPasteService.Verify(p => p.Stop(), Times.Once);

        return Task.CompletedTask;
    }
}
