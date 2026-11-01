using ClipMate.Core.Constants;
using ClipMate.Core.Events;
using ClipMate.Core.Exceptions;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using ClipMate.Data.Services;
using ClipMate.Platform.Helpers;
using ClipMate.Tests.Unit.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// The one path for putting a clip on the clipboard, and the holder of the Current Clip (ADR-0004).
/// </summary>
public class CurrentClipWriterTests
{
    private const string _databaseKey = "db";

    private IClipboardService _clipboard = null!;
    private FakeDatabase _database = null!;
    private StrongReferenceMessenger _messenger = null!;
    private ITemplateService _templateService = null!;
    private CurrentClipWriter _writer = null!;

    // What the clipboard port was asked to write, as the clipboard would hold it
    private readonly List<(Clip Clip, bool Filtered)> _writes = [];
    private readonly List<CurrentClip> _currentClipChanges = [];

    [Before(Test)]
    public void Setup()
    {
        _clipboard = A.Fake<IClipboardService>();
        A.CallTo(() => _clipboard.SetClipboardContentAsync(A<Clip>._, A<bool>._, A<CancellationToken>._))
            .Invokes(call => _writes.Add((call.GetArgument<Clip>(0)!, call.GetArgument<bool>(1))));

        var databases = new FakeDatabases();
        _database = databases.AddLocal(_databaseKey, "My Clips");
        _templateService = A.Fake<ITemplateService>();
        A.CallTo(() => _templateService.TryApplyTemplate(A<Clip>._)).Returns(null);
        _messenger = new StrongReferenceMessenger();
        _messenger.Register<CurrentClipChangedEvent>(this, (_, message) => _currentClipChanges.Add(message.CurrentClip));

        _writer = new CurrentClipWriter(_clipboard, databases, _templateService, _messenger, A.Fake<ILogger<CurrentClipWriter>>());
    }

    [Test]
    public async Task MakeCurrentAsync_StoredClip_WritesItsLoadedContentWithTheHashThatSuppressesCapture()
    {
        // Arrange: the list row has only metadata; the content is in the Database
        var clip = StoreTextClip("Hello from the Database");

        // Act
        var written = await _writer.MakeCurrentAsync(_databaseKey, clip.Id);

        // Assert
        await Assert.That(_writes.Count).IsEqualTo(1);
        await Assert.That(_writes[0].Clip.TextContent).IsEqualTo("Hello from the Database");
        await Assert.That(_writes[0].Clip.ContentHash).IsEqualTo(ContentHasher.HashText("Hello from the Database"));
        await Assert.That(_writes[0].Filtered).IsFalse();
        await Assert.That(written).IsSameReferenceAs(_writes[0].Clip);
    }

    [Test]
    public async Task MakeCurrentAsync_ActiveTemplate_WritesTheTemplatedClip()
    {
        // Arrange
        var clip = StoreTextClip("Hello");
        A.CallTo(() => _templateService.TryApplyTemplate(A<Clip>._))
            .ReturnsLazily(call => call.GetArgument<Clip>(0)!.WithText("Dear Hello"));

        // Act
        await _writer.MakeCurrentAsync(_databaseKey, clip.Id);

        // Assert
        await Assert.That(_writes[0].Clip.TextContent).IsEqualTo("Dear Hello");
    }

    [Test]
    public async Task MakeCurrentAsync_StoredClip_BecomesTheCurrentClipInItsDatabase()
    {
        // Arrange
        var clip = StoreTextClip("Hello");

        // Act
        await _writer.MakeCurrentAsync(_databaseKey, clip.Id);

        // Assert
        await Assert.That(_writer.CurrentClip?.Clip.Id).IsEqualTo(clip.Id);
        await Assert.That(_writer.CurrentClip?.DatabaseKey).IsEqualTo(_databaseKey);
        await Assert.That(_currentClipChanges.Single().Clip.Id).IsEqualTo(clip.Id);
    }

    [Test]
    public async Task MakeCurrentAsync_Written_TellsTheStatusBarTheClipWasCopied()
    {
        // Arrange
        var clip = StoreTextClip("Hello");
        ClipboardCopiedEvent? copied = null;
        _messenger.Register<ClipboardCopiedEvent>(this, (_, message) => copied = message);

        // Act
        await _writer.MakeCurrentAsync(_databaseKey, clip.Id);

        // Assert
        await Assert.That(copied?.Clip.Id).IsEqualTo(clip.Id);
    }

    [Test]
    public async Task PutOnClipboardAsync_ClipboardCannotBeSet_LeavesTheCurrentClipAsItWas()
    {
        // Arrange
        var before = new Clip { Id = Guid.NewGuid(), Title = "Before" };
        _writer.ReportCurrent(before, _databaseKey);
        A.CallTo(() => _clipboard.SetClipboardContentAsync(A<Clip>._, A<bool>._, A<CancellationToken>._))
            .Throws(new ClipboardException("Locked"));

        // Act
        await Assert.That(() => _writer.PutOnClipboardAsync(new Clip { Id = Guid.NewGuid() })).Throws<ClipboardException>();

        // Assert
        await Assert.That(_writer.CurrentClip?.Clip).IsSameReferenceAs(before);
    }

    [Test]
    public async Task MakeCurrentAsync_ClipWithNoContent_WritesNothing()
    {
        // Act
        var written = await _writer.MakeCurrentAsync(_databaseKey, Guid.NewGuid());

        // Assert
        await Assert.That(written).IsNull();
        await Assert.That(_writes).IsEmpty();
        await Assert.That(_writer.CurrentClip).IsNull();
    }

    [Test]
    public async Task MakeCurrentAsync_FilterForced_WritesFiltered()
    {
        // Arrange: Manual Filter
        var clip = StoreTextClip("Hello");

        // Act
        await _writer.MakeCurrentAsync(_databaseKey, clip.Id, new WriteOptions(FilterMode.Forced));

        // Assert
        await Assert.That(_writes[0].Filtered).IsTrue();
    }

    [Test]
    public async Task PutOnClipboardAsync_Content_IsWrittenAsItIsWithoutTheTemplate()
    {
        // Arrange: Place Source URL On Clipboard
        var content = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "https://example.com" };

        // Act
        await _writer.PutOnClipboardAsync(content);

        // Assert
        await Assert.That(_writes.Single().Clip).IsSameReferenceAs(content);
        A.CallTo(() => _templateService.TryApplyTemplate(A<Clip>._)).MustNotHaveHappened();
        await Assert.That(_writer.CurrentClip?.Clip).IsSameReferenceAs(content);
        await Assert.That(_writer.CurrentClip?.DatabaseKey).IsNull();
    }

    [Test]
    public async Task Prepare_PowerPasteClip_ReturnsItsFormatsAndMakesItCurrentWithoutWriting()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Next" };
        IReadOnlyList<ClipboardFormatData> formats = [new((uint)Formats.UnicodeText.Code, [1])];
        A.CallTo(() => _clipboard.GetClipboardFormats(clip, false)).Returns(formats);

        // Act
        var prepared = _writer.Prepare(clip);

        // Assert
        await Assert.That(prepared).IsSameReferenceAs(formats);
        await Assert.That(_writes).IsEmpty();
        await Assert.That(_writer.CurrentClip?.Clip).IsSameReferenceAs(clip);
    }

    [Test]
    public async Task ReportCurrent_CapturedClip_BecomesCurrentWithoutWriting()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Captured" };

        // Act
        _writer.ReportCurrent(clip, _databaseKey);

        // Assert
        await Assert.That(_writes).IsEmpty();
        await Assert.That(_writer.CurrentClip).IsEqualTo(new CurrentClip(clip, _databaseKey));
        await Assert.That(_currentClipChanges.Single()).IsEqualTo(new CurrentClip(clip, _databaseKey));
    }

    private Clip StoreTextClip(string text)
    {
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, Title = text };
        var data = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.UnicodeText.Code, StorageType = StorageType.Text };
        _database.WithContent(new ClipContent(clip, [data], [new BlobTxt { ClipId = clip.Id, ClipDataId = data.Id, Data = text }], [], [], []));

        return new Clip { Id = clip.Id, Type = ClipType.Text, Title = text };
    }
}
