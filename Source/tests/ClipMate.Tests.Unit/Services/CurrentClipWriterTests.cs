using ClipMate.Core.Constants;
using ClipMate.Core.Events;
using ClipMate.Core.Exceptions;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using ClipMate.Data.Services;
using ClipMate.Platform;
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
    private PreferencesConfiguration _preferences = null!;
    private ISoundService _soundService = null!;
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

        var configuration = new ClipMateConfiguration();
        _preferences = configuration.Preferences;
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(configuration);
        _soundService = A.Fake<ISoundService>();

        _writer = new CurrentClipWriter(_clipboard, databases, _templateService, configurationService, _soundService, _messenger,
            A.Fake<ILogger<CurrentClipWriter>>());
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
        _writer.ReportCurrent(before);
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
    public async Task ReportCapturedAsync_FilterOff_BecomesCurrentWithoutWriting()
    {
        // Arrange
        var clip = StoreHtmlClip("Hello", "<b>Hello</b>");

        // Act
        await _writer.ReportCapturedAsync(clip, _databaseKey);

        // Assert
        await Assert.That(_writes).IsEmpty();
        await Assert.That(_writer.CurrentClip).IsEqualTo(new CurrentClip(clip, _databaseKey));
        await Assert.That(_currentClipChanges.Single()).IsEqualTo(new CurrentClip(clip, _databaseKey));
    }

    [Test]
    public async Task ReportCapturedAsync_ActiveFilter_PutsTheCopyBackFilteredWithTheFilterSound()
    {
        // Arrange: an HTML copy from a browser
        _preferences.OutboundFilter = true;
        _preferences.OutboundFilterMode = OutboundFilterMode.Active;
        var clip = StoreHtmlClip("Hello", "<b>Hello</b>");

        // Act
        await _writer.ReportCapturedAsync(clip, _databaseKey);

        // Assert
        await Assert.That(_writes.Single().Filtered).IsTrue();
        await Assert.That(_writes[0].Clip.TextContent).IsEqualTo("Hello");
        A.CallTo(() => _soundService.PlaySoundAsync(SoundEvent.Filter, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ReportCapturedAsync_ActiveFilterWithATemplate_PutsTheCopyBackTemplated()
    {
        // Arrange: as ClipMate 7's ClipObj.Copy, which applies the template to every write
        _preferences.OutboundFilter = true;
        _preferences.OutboundFilterMode = OutboundFilterMode.Active;
        A.CallTo(() => _templateService.TryApplyTemplate(A<Clip>._))
            .ReturnsLazily(call => call.GetArgument<Clip>(0)!.WithText("Dear Hello"));
        var clip = StoreHtmlClip("Hello", "<b>Hello</b>");

        // Act
        await _writer.ReportCapturedAsync(clip, _databaseKey);

        // Assert
        await Assert.That(_writes.Single().Clip.TextContent).IsEqualTo("Dear Hello");
        await Assert.That(_writes[0].Filtered).IsTrue();
    }

    [Test]
    public async Task ReportCapturedAsync_ActiveFilterOnPlainText_LeavesTheClipboardAlone()
    {
        // Arrange: nothing for the filter to drop, so no write and no sound
        _preferences.OutboundFilter = true;
        _preferences.OutboundFilterMode = OutboundFilterMode.Active;
        var clip = StoreTextClip("Hello");

        // Act
        await _writer.ReportCapturedAsync(clip, _databaseKey);

        // Assert
        await Assert.That(_writes).IsEmpty();
        A.CallTo(() => _soundService.PlaySoundAsync(A<SoundEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ReportCapturedAsync_ActiveFilterOnExcludedProgram_LeavesTheClipboardAlone()
    {
        // Arrange: copied in Explorer, excluded by default; the name matches without ".exe" and ignoring case
        _preferences.OutboundFilter = true;
        _preferences.OutboundFilterMode = OutboundFilterMode.Active;
        var clip = StoreHtmlClip("Hello", "<b>Hello</b>", "explorer.exe");

        // Act
        await _writer.ReportCapturedAsync(clip, _databaseKey);

        // Assert
        await Assert.That(_writes).IsEmpty();
    }

    [Test]
    public async Task ReportCapturedAsync_ExclusionIsAPartOfTheName_StillFilters()
    {
        // Arrange: ClipMate 7 matched a substring, so "EXP" excluded Explorer; an entry now matches only the whole name
        _preferences.OutboundFilter = true;
        _preferences.OutboundFilterMode = OutboundFilterMode.Active;
        _preferences.OutboundFilterExclusions = ["EXP"];
        var clip = StoreHtmlClip("Hello", "<b>Hello</b>", "EXPLORER");

        // Act
        await _writer.ReportCapturedAsync(clip, _databaseKey);

        // Assert
        await Assert.That(_writes.Single().Filtered).IsTrue();
    }

    [Test]
    public async Task ReportCapturedAsync_PassiveFilter_WaitsForClipMateToLoseFocus()
    {
        // Arrange
        _preferences.OutboundFilter = true;
        _preferences.OutboundFilterMode = OutboundFilterMode.Passive;
        var clip = StoreHtmlClip("Hello", "<b>Hello</b>");

        // Act
        await _writer.ReportCapturedAsync(clip, _databaseKey);
        var writesBeforeLosingFocus = _writes.Count;
        var filtered = await _writer.FilterCapturedClipAsync();

        // Assert
        await Assert.That(writesBeforeLosingFocus).IsEqualTo(0);
        await Assert.That(filtered).IsTrue();
        await Assert.That(_writes.Single().Filtered).IsTrue();
        A.CallTo(() => _soundService.PlaySoundAsync(SoundEvent.Filter, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task FilterCapturedClipAsync_AlreadyFiltered_DoesNotWriteAgain()
    {
        // Arrange: ClipMate loses focus a second time
        _preferences.OutboundFilter = true;
        _preferences.OutboundFilterMode = OutboundFilterMode.Passive;
        await _writer.ReportCapturedAsync(StoreHtmlClip("Hello", "<b>Hello</b>"), _databaseKey);
        await _writer.FilterCapturedClipAsync();

        // Act
        var filtered = await _writer.FilterCapturedClipAsync();

        // Assert
        await Assert.That(filtered).IsFalse();
        await Assert.That(_writes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task FilterCapturedClipAsync_ClipPickedInClipMate_LeavesItAlone()
    {
        // Arrange: the clip picked was already written as the setting says
        _preferences.OutboundFilter = true;
        _preferences.OutboundFilterMode = OutboundFilterMode.Passive;
        await _writer.ReportCapturedAsync(StoreHtmlClip("Hello", "<b>Hello</b>"), _databaseKey);
        var picked = StoreTextClip("Picked");
        await _writer.MakeCurrentAsync(_databaseKey, picked.Id);

        // Act
        var filtered = await _writer.FilterCapturedClipAsync();

        // Assert
        await Assert.That(filtered).IsFalse();
        await Assert.That(_writes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task FilterCapturedClipAsync_ActiveMode_DoesNothing()
    {
        // Arrange: in Active mode a copy is cleansed when it is captured, not when ClipMate loses focus
        _preferences.OutboundFilter = true;
        _preferences.OutboundFilterMode = OutboundFilterMode.Active;
        await _writer.ReportCapturedAsync(StoreTextClip("Hello"), _databaseKey);

        // Act
        var filtered = await _writer.FilterCapturedClipAsync();

        // Assert
        await Assert.That(filtered).IsFalse();
    }

    [Test]
    public async Task MakeCurrentAsync_FilterOn_WritesFilteredWithoutTheSound()
    {
        // Arrange
        _preferences.OutboundFilter = true;
        var clip = StoreHtmlClip("Hello", "<b>Hello</b>");

        // Act
        await _writer.MakeCurrentAsync(_databaseKey, clip.Id);

        // Assert
        await Assert.That(_writes.Single().Filtered).IsTrue();
        A.CallTo(() => _soundService.PlaySoundAsync(A<SoundEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task MakeCurrentAsync_FilterForcedOnExcludedProgram_WritesItWhole()
    {
        // Arrange: Manual Filter on a clip from an excluded program, as in ClipMate 7
        var clip = StoreHtmlClip("Hello", "<b>Hello</b>", "EXPLORER");

        // Act
        await _writer.MakeCurrentAsync(_databaseKey, clip.Id, new WriteOptions(FilterMode.Forced));

        // Assert
        await Assert.That(_writes.Single().Filtered).IsFalse();
    }

    [Test]
    public async Task PutOnClipboardAsync_FilterOnClipWithNoTextOrImage_WritesItWhole()
    {
        // Arrange: a file list has nothing the filter keeps, so filtering would leave the clipboard empty
        _preferences.OutboundFilter = true;
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Files, FilePathsJson = "[]" };

        // Act
        await _writer.PutOnClipboardAsync(clip);

        // Assert
        await Assert.That(_writes.Single().Filtered).IsFalse();
    }

    [Test]
    public async Task Prepare_FilterOn_OffersTheFilteredFormats()
    {
        // Arrange: Power Paste
        _preferences.OutboundFilter = true;
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Html, TextContent = "Next", HtmlContent = "<b>Next</b>" };

        // Act
        _writer.Prepare(clip);

        // Assert
        A.CallTo(() => _clipboard.GetClipboardFormats(clip, true)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RewriteCurrentAsync_StoredCurrentClip_IsLoadedAndWrittenAsTheSettingSays()
    {
        // Arrange: the Outbound Filter was just turned on
        var clip = StoreHtmlClip("Hello", "<b>Hello</b>");
        await _writer.ReportCapturedAsync(clip, _databaseKey);
        _preferences.OutboundFilter = true;

        // Act
        var rewritten = await _writer.RewriteCurrentAsync();

        // Assert
        await Assert.That(rewritten).IsTrue();
        await Assert.That(_writes.Single().Filtered).IsTrue();
        await Assert.That(_writes[0].Clip.HtmlContent).IsEqualTo("<b>Hello</b>");
        A.CallTo(() => _soundService.PlaySoundAsync(A<SoundEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task RewriteCurrentAsync_NoCurrentClip_WritesNothing()
    {
        // Act
        var rewritten = await _writer.RewriteCurrentAsync(new WriteOptions(FilterMode.Forced));

        // Assert
        await Assert.That(rewritten).IsFalse();
        await Assert.That(_writes).IsEmpty();
    }

    private Clip StoreHtmlClip(string text, string html, string? sourceApplication = null)
    {
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Html, Title = text, SourceApplicationName = sourceApplication };
        var textData = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.UnicodeText.Code, StorageType = StorageType.Text };
        var htmlData = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.Html.Code, StorageType = StorageType.Text };
        _database.WithContent(new ClipContent(clip, [textData, htmlData],
        [
            new BlobTxt { ClipId = clip.Id, ClipDataId = textData.Id, Data = text },
            new BlobTxt { ClipId = clip.Id, ClipDataId = htmlData.Id, Data = html },
        ], [], [], []));

        return new Clip { Id = clip.Id, Type = ClipType.Html, Title = text, SourceApplicationName = sourceApplication };
    }

    private Clip StoreTextClip(string text)
    {
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, Title = text };
        var data = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, Format = Formats.UnicodeText.Code, StorageType = StorageType.Text };
        _database.WithContent(new ClipContent(clip, [data], [new BlobTxt { ClipId = clip.Id, ClipDataId = data.Id, Data = text }], [], [], []));

        return new Clip { Id = clip.Id, Type = ClipType.Text, Title = text };
    }
}
