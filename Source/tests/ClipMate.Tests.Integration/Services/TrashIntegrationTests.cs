using ClipMate.Data.Repositories;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// The Trash keeps a deleted clip's content until it is emptied, so a restored clip comes back intact.
/// </summary>
public class TrashIntegrationTests : CapturePipelineTestBase
{
    [Test]
    public async Task AClipSentToTheTrashAndRestored_KeepsItsText()
    {
        var clip = TextClip("https://example.com/kept");
        await CaptureAsync(clip);

        await ClipRepository.DeleteAsync(clip.Id);
        await ClipRepository.RestoreClipsAsync([clip.Id], Inbox.Id);

        var texts = await new BlobRepository(DbContext).GetTextByClipIdAsync(clip.Id);
        await Assert.That(texts.Select(p => p.Data)).Contains("https://example.com/kept");
    }
}
