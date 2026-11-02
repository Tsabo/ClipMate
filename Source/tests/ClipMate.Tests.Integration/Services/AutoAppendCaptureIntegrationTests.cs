using ClipMate.Core.Models;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Auto-Append through the capture pipeline, as in ClipMate 7: each copy's text goes onto the growing clip, and a copy
/// with no text ends it.
/// </summary>
public class AutoAppendCaptureIntegrationTests : CapturePipelineTestBase
{
    [Test]
    public async Task TextCopy_IsAddedToTheGrowingClipInsteadOfSaved()
    {
        var growing = await StartAutoAppendAsync();

        var copy = TextClip("first line");
        await CaptureAsync(copy, () => WasUpdated(growing.Id));

        await Assert.That(GrowingText(growing)).IsEqualTo("first line");
        await Assert.That(await IsSavedAsync(copy)).IsFalse();
        await Assert.That(AppendMode.IsActive).IsTrue();
    }

    [Test]
    public async Task CopyWithNoText_EndsAutoAppendAndIsSaved()
    {
        await StartAutoAppendAsync();

        var image = new Clip { Id = Guid.NewGuid(), Type = ClipType.Image, ImageData = new byte[5000], CapturedAt = DateTimeOffset.Now };
        await CaptureAsync(image);

        await Assert.That(await IsSavedAsync(image)).IsTrue();
        await Assert.That(AppendMode.IsActive).IsFalse();
    }

    [Test]
    public async Task Capture_AfterGrowingClipSentToTrash_EndsAutoAppendAndIsSaved()
    {
        var growing = await StartAutoAppendAsync();
        await ClipRepository.DeleteAsync(growing.Id);

        var copy = TextClip("https://example.com/second");
        await CaptureAsync(copy);

        await Assert.That(await IsSavedAsync(copy)).IsTrue();
        await Assert.That(AppendMode.IsActive).IsFalse();
    }

    // The empty growing clip the Append command creates
    private async Task<Clip> StartAutoAppendAsync()
    {
        var growing = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            Title = "Append...",
            CollectionId = Inbox.Id,
            CapturedAt = DateTimeOffset.Now,
        };

        await ClipRepository.CreateAsync(growing);
        AppendMode.Start(growing.Id, DatabaseKey);

        return growing;
    }

    private string? GrowingText(Clip growing) =>
        DbContext.BlobTxt.AsEnumerable().FirstOrDefault(p => p.ClipId == growing.Id)?.Data;
}
