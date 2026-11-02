using ClipMate.Core.Models;
using ClipMate.Core.Models.Capture;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Platform.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Every capture decision in ClipMate 7's order (ADR-0005): attempt in, verdict out.
/// </summary>
public class CapturePolicyTests
{
    private const string _db = "db";

    private AppendMode _appendMode = null!;
    private IClipRepository _clipRepository = null!;
    private CapturePolicy _policy = null!;
    private ICaptureTargetResolver _targetResolver = null!;
    private CaptureTarget _target = null!;

    [Before(Test)]
    public void Setup()
    {
        _appendMode = new AppendMode();
        _target = new CaptureTarget(_db, new Collection { Id = Guid.NewGuid(), Title = "Inbox" }, Guid.NewGuid());
        _targetResolver = A.Fake<ICaptureTargetResolver>();
        A.CallTo(() => _targetResolver.ResolveAsync(A<CancellationToken>._)).Returns(_target);

        _clipRepository = A.Fake<IClipRepository>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(_db)).Returns(_clipRepository);

        _policy = new CapturePolicy(_appendMode, _targetResolver, contextFactory, A.Fake<ILogger<CapturePolicy>>());
    }

    [Test]
    [Arguments(CaptureKind.Monitored, true)]
    [Arguments(CaptureKind.Startup, true)]
    [Arguments(CaptureKind.CaptureHotkey, false)]
    [Arguments(CaptureKind.CaptureSpecial, false)]
    public async Task Clip_IsSavedToTheTarget_WithDuplicateCheckedExceptForTheHotkeyAndCaptureSpecial(CaptureKind kind, bool rejectDuplicate)
    {
        // Arrange
        var clip = TextClip("Hello");

        // Act
        var verdict = await _policy.DecideAsync(new CaptureAttempt(clip, "Hello", "NOTEPAD", kind, 12));

        // Assert
        await Assert.That(verdict).IsEqualTo(new SaveVerdict(clip, _target, rejectDuplicate, false));
    }

    [Test]
    public async Task NoAcceptableFormats_IsRejected()
    {
        // Act: the Application Profile kept nothing
        var verdict = await _policy.DecideAsync(new CaptureAttempt(null, "Hello", "NOTEPAD", CaptureKind.Monitored, 12));

        // Assert
        await Assert.That(verdict).IsEqualTo(new RejectedVerdict(CaptureRejection.NoAcceptableFormats));
    }

    [Test]
    public async Task SmallBitmapFromNoOwner_IsRejected()
    {
        // Arrange: ClipMate 7's "WINDOWS" creator, a bitmap and nothing over 4 KB, as when a toolbar button is copied
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Image, ImageData = [1, 2, 3] };

        // Act
        var verdict = await _policy.DecideAsync(new CaptureAttempt(clip, null, ClipboardService.NoOwnerApplication, CaptureKind.Monitored, 1064));

        // Assert
        await Assert.That(verdict).IsEqualTo(new RejectedVerdict(CaptureRejection.SmallGraphic));
    }

    [Test]
    [Arguments(ClipboardService.NoOwnerApplication, 5000, CaptureKind.Monitored)]
    [Arguments("MSPAINT", 1064, CaptureKind.Monitored)]
    [Arguments(ClipboardService.NoOwnerApplication, 1064, CaptureKind.CaptureSpecial)]
    public async Task BitmapThatIsNotASmallGraphic_IsSaved(string source, int largestFormatSize, CaptureKind kind)
    {
        // Arrange: bigger than 4 KB, from a program, or chosen with Capture Special
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Image, ImageData = [1, 2, 3] };

        // Act
        var verdict = await _policy.DecideAsync(new CaptureAttempt(clip, null, source, kind, largestFormatSize));

        // Assert
        await Assert.That(verdict).IsTypeOf<SaveVerdict>();
    }

    [Test]
    public async Task TargetThatCannotBeChosen_IsRejectedAsAnError()
    {
        // Arrange: today the clip would be saved with no collection and lost from the tree
        A.CallTo(() => _targetResolver.ResolveAsync(A<CancellationToken>._)).Throws(new InvalidOperationException("No collections"));

        // Act
        var verdict = await _policy.DecideAsync(new CaptureAttempt(TextClip("Hello"), "Hello", "NOTEPAD", CaptureKind.Monitored, 12));

        // Assert
        await Assert.That(verdict).IsEqualTo(new RejectedVerdict(CaptureRejection.Error, "No collections"));
    }

    [Test]
    public async Task AutoAppend_TextIsAddedToTheGrowingClip()
    {
        // Arrange
        var growingId = StartAutoAppend();

        // Act
        var verdict = await _policy.DecideAsync(new CaptureAttempt(TextClip("more"), "more", "NOTEPAD", CaptureKind.Monitored, 10));

        // Assert
        await Assert.That(verdict).IsEqualTo(new AppendVerdict(_db, growingId, "more"));
    }

    [Test]
    public async Task AutoAppend_ComesBeforeTheApplicationProfile()
    {
        // Arrange: the profile kept nothing, but the clipboard has text, as ClipMate 7 appends CF_TEXT straight away
        var growingId = StartAutoAppend();

        // Act
        var verdict = await _policy.DecideAsync(new CaptureAttempt(null, "more", "NOTEPAD", CaptureKind.Monitored, 10));

        // Assert
        await Assert.That(verdict).IsEqualTo(new AppendVerdict(_db, growingId, "more"));
    }

    [Test]
    public async Task AutoAppend_CopyWithNoText_EndsItAndIsSaved()
    {
        // Arrange
        StartAutoAppend();
        var image = new Clip { Id = Guid.NewGuid(), Type = ClipType.Image, ImageData = new byte[10] };

        // Act
        var verdict = await _policy.DecideAsync(new CaptureAttempt(image, null, "MSPAINT", CaptureKind.Monitored, 9000));

        // Assert
        await Assert.That(verdict).IsEqualTo(new SaveVerdict(image, _target, true, true));
    }

    [Test]
    public async Task AutoAppend_GrowingClipGone_EndsItAndIsSaved()
    {
        // Arrange: the growing clip was sent to the Trash
        var growingId = StartAutoAppend();
        A.CallTo(() => _clipRepository.GetByIdAsync(growingId, A<CancellationToken>._)).Returns((Clip?)null);
        var clip = TextClip("more");

        // Act
        var verdict = await _policy.DecideAsync(new CaptureAttempt(clip, "more", "NOTEPAD", CaptureKind.Monitored, 10));

        // Assert
        await Assert.That(verdict).IsEqualTo(new SaveVerdict(clip, _target, true, true));
    }

    private Guid StartAutoAppend()
    {
        var growingId = Guid.NewGuid();
        A.CallTo(() => _clipRepository.GetByIdAsync(growingId, A<CancellationToken>._)).Returns(new Clip { Id = growingId });
        _appendMode.Start(growingId, _db);

        return growingId;
    }

    private static Clip TextClip(string text) => new() { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = text };
}
