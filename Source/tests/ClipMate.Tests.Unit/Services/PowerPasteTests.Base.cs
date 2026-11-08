using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using ClipMate.Platform;
using ClipMate.Tests.Unit.Networking;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

[Category("PowerPaste")]
[Category("Unit")]
public partial class PowerPasteTests
{
    private InMemoryPowerPasteClipboard _clipboard = null!;
    private ManualTimeProvider _clock = null!;
    private IClipboardService _mockClipboardService = null!;
    private IConfigurationService _mockConfigService = null!;
    private ICurrentClipWriter _mockCurrentClipWriter = null!;
    private ILogger<PowerPaste> _mockLogger = null!;
    private ISoundService _mockSoundService = null!;
    private ITemplateService _mockTemplateService = null!;
    private PowerPasteStatus _status = null!;
    private Clip? _waitingClip;

    [Before(Test)]
    public void Setup()
    {
        _clipboard = new InMemoryPowerPasteClipboard();
        _clock = new ManualTimeProvider();
        _mockClipboardService = A.Fake<IClipboardService>();
        _mockLogger = A.Fake<ILogger<PowerPaste>>();
        _mockConfigService = A.Fake<IConfigurationService>();
        _mockSoundService = A.Fake<ISoundService>();
        _mockTemplateService = A.Fake<ITemplateService>();
        _status = new PowerPasteStatus();
        _waitingClip = null;

        A.CallTo(() => _mockTemplateService.PreviewTemplateText(A<Clip>.Ignored)).Returns(null);

        // The writer prepares each clip's Formats: one per clip, holding its text, so offers can be told apart
        _mockCurrentClipWriter = A.Fake<ICurrentClipWriter>();
        A.CallTo(() => _mockCurrentClipWriter.Prepare(A<Clip>.Ignored, A<WriteOptions>.Ignored))
            .ReturnsLazily((Clip clip, WriteOptions _) =>
            {
                _waitingClip = clip;

                return [new ClipboardFormatData(1, System.Text.Encoding.Unicode.GetBytes(clip.TextContent ?? string.Empty))];
            });

        A.CallTo(() => _mockCurrentClipWriter.ReportCurrent(A<Clip>.Ignored))
            .Invokes((Clip clip) => _waitingClip = clip);

        // Configure sound service to return completed task (with or without cancellation token)
        A.CallTo(() => _mockSoundService.PlaySoundAsync(A<SoundEvent>.Ignored))
            .Returns(Task.CompletedTask);

        A.CallTo(() => _mockSoundService.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Default configuration: no delay after a paste, so a paste moves the sequence on at once
        UseConfiguration(new PreferencesConfiguration
        {
            PowerPasteLoop = false,
            PowerPasteDelimiter = "\n",
            PowerPasteTrim = true,
            PowerPasteIncludeDelimiter = false,
            PowerPasteDelay = 0,
        });
    }

    private PowerPaste CreateService(ManualTimeProvider? clock = null)
    {
        if (clock != null)
            _clock = clock;

        return new PowerPaste(
            _clipboard,
            _mockClipboardService,
            _mockCurrentClipWriter,
            _mockTemplateService,
            _mockConfigService,
            _mockSoundService,
            _mockLogger,
            _status,
            _clock);
    }

    private void UseConfiguration(PreferencesConfiguration preferences) =>
        A.CallTo(() => _mockConfigService.Configuration).Returns(new ClipMateConfiguration { Preferences = preferences });

    private void UseExplode() => _mockConfigService.Configuration.Preferences.PowerPasteExplode = true;

    /// <summary>
    /// The user pastes, well after any earlier paste, into the application they are working in, and the
    /// PowerPasteDelay setting passes.
    /// </summary>
    private Task PasteAsync()
    {
        _clock.Advance(TimeSpan.FromSeconds(1));
        _clipboard.ReadByTheTarget();
        _clock.Advance(TimeSpan.FromMilliseconds(Math.Max(0, _mockConfigService.Configuration.Preferences.PowerPasteDelay)));

        return Task.CompletedTask;
    }

    /// <summary>
    /// The clip waiting to be pasted, as it was last offered; null when Power Paste is not active.
    /// </summary>
    private Clip? WaitingClip(IPowerPaste powerPaste) =>
        powerPaste.State == PowerPasteState.Active
            ? _waitingClip
            : null;

    private int CompleteSoundCount() =>
        Fake.GetCalls(_mockSoundService).Count(p => p.Method.Name == nameof(ISoundService.PlaySoundAsync) && Equals(p.Arguments[0], SoundEvent.PowerPasteComplete));

    private static Clip CreateTestClip(string textContent,
        Guid? id = null,
        Guid? collectionId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TextContent = textContent,
        Type = ClipType.Text,
        CapturedAt = DateTimeOffset.Now,
        CollectionId = collectionId ?? Guid.NewGuid(),
        Title = "Test Clip",
        SourceApplicationName = "TestApp",
    };
}
