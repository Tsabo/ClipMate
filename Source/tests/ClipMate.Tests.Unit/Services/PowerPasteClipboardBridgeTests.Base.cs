using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

[Category("PowerPasteClipboardBridge")]
[Category("Unit")]
public partial class PowerPasteClipboardBridgeTests
{
    private Mock<IClipboardService> _mockClipboardService = null!;
    private Mock<IConfigurationService> _mockConfigService = null!;
    private Mock<ILogger<PowerPasteClipboardBridge>> _mockLogger = null!;
    private Mock<IPowerPasteService> _mockPowerPasteService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockPowerPasteService = new Mock<IPowerPasteService>();
        _mockClipboardService = new Mock<IClipboardService>();
        _mockLogger = new Mock<ILogger<PowerPasteClipboardBridge>>();
        _mockConfigService = new Mock<IConfigurationService>();

        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration { PowerPasteDelay = 200 },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        _mockClipboardService.Setup(p => p.SetClipboardContentAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPowerPasteService.Setup(p => p.AdvanceToNextAsync()).Returns(Task.CompletedTask);
    }

    private PowerPasteClipboardBridge CreateBridge() => new(
        _mockPowerPasteService.Object,
        _mockClipboardService.Object,
        _mockConfigService.Object,
        _mockLogger.Object);

    /// <summary>
    /// OnPasteDetected advances the sequence on a background Task.Run after a real-time debounce
    /// delay, so tests must wait for it rather than assume a fixed sleep completes in time - CI
    /// runners under load can stall the threadpool well past the nominal delay.
    /// </summary>
    private async Task WaitForAdvanceCallsAsync(int expectedCount, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var actual = _mockPowerPasteService.Invocations.Count(i => i.Method.Name == nameof(IPowerPasteService.AdvanceToNextAsync));
            if (actual >= expectedCount)
                return;

            await Task.Delay(20);
        }
    }

    private static Clip CreateTextClip(string text = "Hello") => new()
    {
        Id = Guid.NewGuid(),
        TextContent = text,
        Type = ClipType.Text,
        CapturedAt = DateTimeOffset.Now,
        CollectionId = Guid.NewGuid(),
        Title = "Test Clip",
    };

    private static Clip CreateImageClip() => new()
    {
        Id = Guid.NewGuid(),
        Type = ClipType.Image,
        ImageData = [1, 2, 3],
        CapturedAt = DateTimeOffset.Now,
        CollectionId = Guid.NewGuid(),
        Title = "Test Image",
    };
}
