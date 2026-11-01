using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Duplicates through the capture pipeline. As in ClipMate 7, a capture is a Duplicate when its plain text is the same
/// as the newest clip in the collection it is going into; a collection that does not accept duplicates rejects it.
/// </summary>
public class DuplicateCaptureIntegrationTests : CapturePipelineTestBase
{
    private const string _link = "https://example.com/page";

    [Test]
    public async Task SavedCapture_BecomesTheCurrentClip()
    {
        var clip = TextClip(_link);
        await CaptureAsync(clip, () => ReportedCapturedCount(clip.Id) > 0);

        await Assert.That(ReportedCapturedCount(clip.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Duplicate_LeavesTheClipAlreadyThereCurrent()
    {
        // As in ClipMate 7, so the Outbound Filter can still put the clipboard back filtered
        var first = TextClip(_link);
        await CaptureAsync(first, () => ReportedCapturedCount(first.Id) > 0);

        await CaptureAsync(TextClip(_link), () => ReportedCapturedCount(first.Id) > 1);

        await Assert.That(ReportedCapturedCount(first.Id)).IsEqualTo(2);
    }

    [Test]
    public async Task SameTextAsAClipSentToTheTrash_IsSaved()
    {
        var first = TextClip(_link);
        await CaptureAsync(first);
        await ClipRepository.DeleteAsync(first.Id);

        var again = TextClip(_link);
        await CaptureAsync(again);

        await Assert.That(await IsSavedAsync(again)).IsTrue();
    }

    [Test]
    public async Task SameTextAsTheNewestClip_IsRejected()
    {
        await CaptureAsync(TextClip(_link));

        var again = TextClip(_link);
        await CaptureAsync(again);

        await Assert.That(await IsSavedAsync(again)).IsFalse();
    }

    [Test]
    public async Task SameTextAsAnOlderClip_IsSaved()
    {
        await CaptureAsync(TextClip(_link));
        await CaptureAsync(TextClip("something copied since"));

        var again = TextClip(_link);
        await CaptureAsync(again);

        await Assert.That(await IsSavedAsync(again)).IsTrue();
    }

    [Test]
    public async Task SameTextAsTheNewestClipOfAnotherCollection_IsSaved()
    {
        ActiveCollection = await AddCollectionAsync("Safe", acceptDuplicates: false);
        await CaptureAsync(TextClip(_link));

        ActiveCollection = Inbox;
        var again = TextClip(_link);
        await CaptureAsync(again);

        await Assert.That(await IsSavedAsync(again)).IsTrue();
    }

    [Test]
    public async Task AClipWithoutPlainText_IsNeverADuplicate()
    {
        var pixels = new byte[] { 1, 2, 3, 4 };
        await CaptureAsync(ImageClip(pixels));

        var again = ImageClip(pixels);
        await CaptureAsync(again);

        await Assert.That(await IsSavedAsync(again)).IsTrue();
    }

    [Test]
    public async Task ACollectionThatAcceptsDuplicates_SavesANewClipEveryTime()
    {
        ActiveCollection = await AddCollectionAsync("Numbers", acceptDuplicates: true);
        var first = TextClip("42");
        await CaptureAsync(first);

        var again = TextClip("42");
        await CaptureAsync(again);

        await Assert.That(await IsSavedAsync(first)).IsTrue();
        await Assert.That(await IsSavedAsync(again)).IsTrue();
    }

    [Test]
    public async Task AManualCapture_IsNeverRejectedAsADuplicate()
    {
        await CaptureAsync(TextClip(_link));

        var again = TextClip(_link);
        await ManualCaptureAsync(again);

        await Assert.That(await IsSavedAsync(again)).IsTrue();
    }

    private static Clip ImageClip(byte[] pixels) => new()
    {
        Id = Guid.NewGuid(),
        Type = ClipType.Image,
        ImageData = pixels,
        ContentHash = Convert.ToHexString(pixels),
        CapturedAt = DateTimeOffset.Now,
    };

    private int ReportedCapturedCount(Guid clipId) =>
        Fake.GetCalls(CurrentClipWriter).Count(p => p.Method.Name == nameof(ICurrentClipWriter.ReportCapturedAsync) &&
                                                     p.GetArgument<Clip>(0)?.Id == clipId);
}
