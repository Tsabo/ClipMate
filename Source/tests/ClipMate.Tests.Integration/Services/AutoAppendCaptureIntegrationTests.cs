namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Auto-Append through the capture pipeline.
/// </summary>
public class AutoAppendCaptureIntegrationTests : CapturePipelineTestBase
{
    [Test]
    public async Task Capture_AfterGrowingClipSentToTrash_IsSavedAndBecomesTheGrowingClip()
    {
        AutoAppendService.Activate();
        var first = TextClip("https://example.com/first");
        await CaptureAsync(first, () => AutoAppendService.GrowingClipId == first.Id);
        await Assert.That(AutoAppendService.GrowingClipId).IsEqualTo(first.Id);

        await ClipRepository.DeleteAsync(first.Id);

        var second = TextClip("https://example.com/second");
        await CaptureAsync(second, () => AutoAppendService.GrowingClipId == second.Id);

        await Assert.That(await IsSavedAsync(second)).IsTrue();
        await Assert.That(AutoAppendService.GrowingClipId).IsEqualTo(second.Id);
    }
}
