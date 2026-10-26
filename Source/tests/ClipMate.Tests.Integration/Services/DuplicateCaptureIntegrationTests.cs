using ClipMate.Core.Models;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Duplicates through the capture pipeline. As in ClipMate 7, a capture is a Duplicate when its plain text is the same
/// as the newest clip in the collection it is going into; a collection that does not accept duplicates rejects it.
/// </summary>
public class DuplicateCaptureIntegrationTests : CapturePipelineTestBase
{
    private const string _link = "https://example.com/page";

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
}
