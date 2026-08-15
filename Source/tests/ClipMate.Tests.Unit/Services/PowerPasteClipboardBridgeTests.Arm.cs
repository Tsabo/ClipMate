using ClipMate.Core.Models;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteClipboardBridgeTests
{
    [Test]
    [Category("Arm")]
    public Task Arm_WithImageClip_SetsClipboardImmediately()
    {
        // Arrange
        var bridge = CreateBridge();
        var clip = CreateImageClip();

        // Act
        bridge.Arm(clip);

        // Assert
        _mockClipboardService.Verify(p => p.SetClipboardContentAsync(clip, It.IsAny<CancellationToken>()), Times.Once);

        return Task.CompletedTask;
    }

    [Test]
    [Category("Arm")]
    public Task Arm_WithTextClip_DoesNotSetClipboardImmediately()
    {
        // Arrange - text clips are delayed-rendered, not pushed immediately
        var bridge = CreateBridge();
        var clip = CreateTextClip();

        // Act
        bridge.Arm(clip);

        // Assert
        _mockClipboardService.Verify(p => p.SetClipboardContentAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Never);

        return Task.CompletedTask;
    }

    [Test]
    [Category("Arm")]
    public async Task Arm_WithNullClip_DoesNotThrow()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act & Assert
        await Assert.That(() => bridge.Arm(null)).ThrowsNothing();
    }

    [Test]
    [Category("Arm")]
    public async Task Arm_WithoutActiveWindow_DoesNotThrow()
    {
        // Arrange - no message window exists yet (PowerPaste not Active), text clip should
        // no-op the Win32 registration rather than throw
        var bridge = CreateBridge();
        var clip = CreateTextClip();

        // Act & Assert
        await Assert.That(() => bridge.Arm(clip)).ThrowsNothing();
    }
}
