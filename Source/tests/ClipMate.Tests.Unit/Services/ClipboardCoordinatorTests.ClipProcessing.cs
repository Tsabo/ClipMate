using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Capture;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using ClipMate.Platform;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// The coordinator acts on the Capture Policy's verdict: it saves through the clip store, appends or drops, with
/// ClipMate 7's sound for the outcome (ADR-0005). The policy's rules have their own tests.
/// </summary>
public partial class ClipboardCoordinatorTests
{
    private const string _db = "db";

    [Test]
    public async Task SaveVerdict_ClipAdded_PlaysTheCaptureSoundAndMakesItCurrent()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Hello" };
        var harness = new Harness(new SaveVerdict(clip, Target(), true, false));
        A.CallTo(() => harness.Store.AddAsync(_db, clip, A<Guid?>._, A<Guid?>._, true, A<CancellationToken>._)).Returns(new ClipAdded(clip));

        // Act
        await harness.CaptureAsync(clip);

        // Assert
        A.CallTo(() => harness.Sound.PlaySoundAsync(SoundEvent.ClipboardUpdate, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Writer.ReportCapturedAsync(clip, _db, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        await Assert.That(harness.Added.Single().Clip).IsSameReferenceAs(clip);
    }

    [Test]
    public async Task SaveVerdict_DuplicateFound_PlaysIgnoreAndKeepsTheClipAlreadyThereCurrent()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Hello" };
        var existing = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Hello" };
        var harness = new Harness(new SaveVerdict(clip, Target(), true, false));
        A.CallTo(() => harness.Store.AddAsync(_db, clip, A<Guid?>._, A<Guid?>._, true, A<CancellationToken>._)).Returns(new DuplicateFound(existing));

        // Act
        await harness.CaptureAsync(clip);

        // Assert
        A.CallTo(() => harness.Sound.PlaySoundAsync(SoundEvent.Ignore, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Writer.ReportCapturedAsync(existing, _db, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        await Assert.That(harness.Added).IsEmpty();
    }

    [Test]
    public async Task SaveVerdict_EndingAutoAppend_TurnsItOff()
    {
        // Arrange: a copy with no text while Auto-Append is on
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Image, ImageData = [1] };
        var harness = new Harness(new SaveVerdict(clip, Target(), true, true));
        A.CallTo(() => harness.Store.AddAsync(A<string>._, clip, A<Guid?>._, A<Guid?>._, A<bool>._, A<CancellationToken>._)).Returns(new ClipAdded(clip));

        // Act
        await harness.CaptureAsync(clip);

        // Assert
        A.CallTo(() => harness.AppendMode.Stop()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RejectedVerdict_PlaysEraseAndKeepsNothing()
    {
        // Arrange: the Application Profile kept nothing
        var harness = new Harness(new RejectedVerdict(CaptureRejection.NoAcceptableFormats));

        // Act
        await harness.CaptureAsync(null);

        // Assert
        A.CallTo(() => harness.Sound.PlaySoundAsync(SoundEvent.Erase, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Store.AddAsync(A<string>._, A<Clip>._, A<Guid?>._, A<Guid?>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task AppendVerdict_AddsTheTextToTheGrowingClip()
    {
        // Arrange
        var growingId = Guid.NewGuid();
        var harness = new Harness(new AppendVerdict(_db, growingId, "more"));
        A.CallTo(() => harness.Append.AppendCapturedTextAsync(growingId, "more", A<string>._, A<bool>._, A<CancellationToken>._))
            .Returns(new Clip { Id = growingId, TextContent = "first\nmore" });

        // Act
        await harness.CaptureAsync(new Clip { Id = Guid.NewGuid(), TextContent = "more" });

        // Assert
        A.CallTo(() => harness.Append.AppendCapturedTextAsync(growingId, "more", A<string>._, A<bool>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Store.AddAsync(A<string>._, A<Clip>._, A<Guid?>._, A<Guid?>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private static CaptureTarget Target() => new(_db, new Collection { Id = Guid.NewGuid(), Title = "Inbox" }, null);

    /// <summary>
    /// A coordinator whose policy always returns one verdict, with every collaborator faked.
    /// </summary>
    private sealed class Harness
    {
        private readonly ClipboardCoordinator _coordinator;

        public Harness(CaptureVerdict verdict)
        {
            var clipboard = A.Fake<IClipboardService>();

            var configuration = A.Fake<IConfigurationService>();
            A.CallTo(() => configuration.Configuration).Returns(new Core.Models.Configuration.ClipMateConfiguration
            {
                Preferences = new Core.Models.Configuration.PreferencesConfiguration { EnableAutoCaptureAtStartup = true, CaptureExistingClipboardAtStartup = false },
            });

            var policy = A.Fake<ICapturePolicy>();
            A.CallTo(() => policy.DecideAsync(A<CaptureAttempt>._, A<CancellationToken>._)).Returns(verdict);

            var services = new ServiceCollection();
            services.AddSingleton(policy);
            services.AddSingleton(Sound);
            services.AddSingleton(Append);
            var messenger = new StrongReferenceMessenger();
            messenger.Register<ClipAddedEvent>(this, (_, message) => Added.Add(message));

            _coordinator = new ClipboardCoordinator(clipboard, configuration, Writer, Store, AppendMode, services.BuildServiceProvider(), messenger,
                A.Fake<Microsoft.Extensions.Logging.ILogger<ClipboardCoordinator>>());
        }

        public IClipStore Store { get; } = A.Fake<IClipStore>();

        public ISoundService Sound { get; } = A.Fake<ISoundService>();

        public IClipAppendService Append { get; } = A.Fake<IClipAppendService>();

        public ICurrentClipWriter Writer { get; } = A.Fake<ICurrentClipWriter>();

        public IAppendMode AppendMode { get; } = A.Fake<IAppendMode>();

        public List<ClipAddedEvent> Added { get; } = [];

        // The step the channel loop runs for each capture, without starting the loop
        public Task CaptureAsync(Clip? clip) =>
            _coordinator.ProcessAttemptAsync(new CaptureAttempt(clip, clip?.TextContent, null, CaptureKind.Monitored, 0), CancellationToken.None);
    }
}
