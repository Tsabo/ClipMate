using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

[Category("PowerPasteClipboardBridge")]
[Category("Unit")]
public partial class PowerPasteClipboardBridgeTests
{
    private IClipboardService _mockClipboardService = null!;
    private IConfigurationService _mockConfigService = null!;
    private ILogger<PowerPasteClipboardBridge> _mockLogger = null!;
    private IPowerPasteService _mockPowerPasteService = null!;
    private ITemplateService _mockTemplateService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockPowerPasteService = A.Fake<IPowerPasteService>();
        _mockClipboardService = A.Fake<IClipboardService>();
        _mockLogger = A.Fake<ILogger<PowerPasteClipboardBridge>>();
        _mockConfigService = A.Fake<IConfigurationService>();
        _mockTemplateService = A.Fake<ITemplateService>();
        A.CallTo(() => _mockTemplateService.PreviewTemplateText(A<Clip>.Ignored)).Returns(null);

        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration { PowerPasteDelay = 200 },
        };

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        A.CallTo(() => _mockClipboardService.SetClipboardContentAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        A.CallTo(() => _mockPowerPasteService.AdvanceToNextAsync()).Returns(Task.CompletedTask);
    }

    private PowerPasteClipboardBridge CreateBridge() => new(
        _mockPowerPasteService,
        _mockClipboardService,
        _mockConfigService,
        _mockTemplateService,
        _mockLogger);

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
            var actual = Fake.GetCalls(_mockPowerPasteService).Count(i => i.Method.Name == nameof(IPowerPasteService.AdvanceToNextAsync));
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
