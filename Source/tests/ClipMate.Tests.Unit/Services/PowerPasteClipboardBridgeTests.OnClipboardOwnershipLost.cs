using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteClipboardBridgeTests
{
    [Test]
    [Category("OnClipboardOwnershipLost")]
    public Task OnClipboardOwnershipLost_StopsPowerPasteAndLeavesTheNewCopyAlone()
    {
        // Arrange - simulates the user copying something new elsewhere while PowerPaste is active
        var bridge = CreateBridge();

        // Act
        bridge.OnClipboardOwnershipLost();

        // Assert
        A.CallTo(() => _mockPowerPasteService.Stop(false)).MustHaveHappenedOnceExactly();

        return Task.CompletedTask;
    }
}
