using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Power Paste: pastes a sequence of clips, or the Explode pieces of them, one per paste. The sequence is kept here;
/// the waiting clip is offered through <see cref="IPowerPasteClipboard" /> by delayed rendering, and each read it
/// reports is either a paste, which moves the sequence on, or a read by something else, after which the clip is
/// offered again. Capture is held while Power Paste runs, since every offer is a clipboard change.
/// </summary>
public sealed class PowerPaste : IPowerPaste, IDisposable
{
    // ClipMate 7 beeps twice, this far apart, when a looping sequence starts over
    private static readonly TimeSpan _loopBeepInterval = TimeSpan.FromMilliseconds(250);

    // Browsers, Electron programs and Windows Terminal read without a window, and a Store app's read comes from a Windows
    // service, so the reader cannot be tied to the application in the foreground. Those reads are a paste when the user
    // just did something: this recently...
    private static readonly TimeSpan _inputBeforePaste = TimeSpan.FromSeconds(1);

    // ...and after the clip came up. The key press that pasted the previous clip is released about now, so input this
    // soon after the offer still belongs to that paste, and a clipboard monitor reading the new clip is not a paste.
    private static readonly TimeSpan _inputAfterOffer = TimeSpan.FromMilliseconds(150);

    // Some applications read the clipboard again shortly after a paste: Word 126 ms later and Excel 168 ms later when
    // measured. A read by the target arriving this soon is one of those, not a second paste. ClipMate 7 has no such limit,
    // only PowerPasteDelay, so this is as short as it can be without skipping clips; pasting every 300 ms still advances.
    private static readonly TimeSpan _minPasteInterval = TimeSpan.FromMilliseconds(250);

    // A clip already rendered for a background reader hides a paste until it is offered again, so this wait is a window
    // in which a paste goes unnoticed. Remote Desktop's clipboard client, which WSLg also runs, reads within milliseconds
    // of each offer for a burst of a few reads: at 100 ms that burst filled most of the time between two pastes. This
    // wait only has to let the reader close the clipboard.
    private static readonly TimeSpan _reOfferSettleTime = TimeSpan.FromMilliseconds(25);

    // Offering again changes the clipboard, and a reader that ignores the "ignore me" markers reads every change. Those
    // bursts usually end by themselves, and holding offers back during one hides pastes, so the limit is well above a
    // burst: it only keeps a reader that never stops from spinning with us. Offers beyond it wait for the interval.
    private static readonly TimeSpan _reOfferInterval = TimeSpan.FromSeconds(1);
    private const int QuickReOffersPerInterval = 20;

    // Remote Desktop's clipboard client, which WSLg also runs, can keep this up for as long as Power Paste is on: on a busy
    // machine it read 15 times a second, below the limit above. Every offer wakes each clipboard monitor on the machine, and
    // they hold the clipboard often enough that 5% of attempts to open it failed. A text box then pastes nothing, and a
    // paste landing between a read and the next offer goes unnoticed. A named program reading this often is not doing it
    // for the user, so it gets nothing until Power Paste next starts, which ends the changes that keep it reading.
    private static readonly TimeSpan _endlessReaderWindow = TimeSpan.FromSeconds(2);
    private const int EndlessReaderReads = 10;

    private readonly Dictionary<string, Queue<DateTimeOffset>> _backgroundReads = [];
    private readonly IPowerPasteClipboard _clipboard;
    private readonly IClipboardService _clipboardService;
    private readonly IConfigurationService _configurationService;
    private readonly ICurrentClipWriter _currentClipWriter;
    private readonly HashSet<string> _endlessReaders = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<PowerPaste> _logger;
    private readonly Queue<DateTimeOffset> _recentReOffers = new();
    private readonly List<PowerPasteStep> _sequence = [];
    private readonly ISoundService _soundService;
    private readonly PowerPasteStatus _status;
    private readonly ITemplateService _templateService;
    private readonly TimeProvider _timeProvider;

    // Sequence state, guarded by _lock
    private bool _isSingleClip;
    private Func<Clip, Task>? _loadContent;
    private int _pasteCount;

    // Clipboard state, used on the UI thread only. The generation moves on with every clip offered and when the
    // clipboard is released, so a re-offer scheduled for an earlier one is dropped.
    private CancellationTokenSource? _advanceDelay;
    private int _generation;
    private bool _isHolding;
    private DateTimeOffset _clipOfferedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastPasteAt = DateTimeOffset.MinValue;
    private bool _reOfferPending;
    private Clip? _waitingClip;
    private bool _waitingClipIsTemplated;

    public PowerPaste(IPowerPasteClipboard clipboard,
        IClipboardService clipboardService,
        ICurrentClipWriter currentClipWriter,
        ITemplateService templateService,
        IConfigurationService configurationService,
        ISoundService soundService,
        ILogger<PowerPaste> logger,
        PowerPasteStatus? status = null,
        TimeProvider? timeProvider = null)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _currentClipWriter = currentClipWriter ?? throw new ArgumentNullException(nameof(currentClipWriter));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _status = status ?? new PowerPasteStatus();
        _timeProvider = timeProvider ?? TimeProvider.System;

        _clipboard.Read += OnClipboardRead;
        _clipboard.Lost += OnClipboardLost;
    }

    private PreferencesConfiguration Preferences => _configurationService.Configuration.Preferences;

    /// <inheritdoc />
    public PowerPasteState State { get; private set; } = PowerPasteState.Inactive;

    /// <inheritdoc />
    public PowerPasteDirection Direction { get; private set; } = PowerPasteDirection.Down;

    /// <summary>
    /// Gets the position of the waiting clip in the sequence, or -1 when Power Paste is not active.
    /// </summary>
    public int CurrentPosition { get; private set; } = -1;

    /// <inheritdoc />
    public int TotalCount => _sequence.Count;

    /// <inheritdoc />
    public event EventHandler<PowerPasteStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<PowerPastePositionChangedEventArgs>? PositionChanged;

    /// <inheritdoc />
    public void Dispose()
    {
        _clipboard.Read -= OnClipboardRead;
        _clipboard.Lost -= OnClipboardLost;
        _advanceDelay?.Cancel();
        _advanceDelay?.Dispose();
        _lock.Dispose();
    }

    /// <inheritdoc />
    public async Task StartAsync(IReadOnlyList<Clip> clips, PowerPasteDirection direction, Func<Clip, Task>? loadContent = null, CancellationToken cancellationToken = default)
    {
        if (clips == null || clips.Count == 0)
            throw new ArgumentException("Clips collection cannot be null or empty", nameof(clips));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var preferences = Preferences;
            var explode = preferences.PowerPasteExplode;
            _logger.LogInformation("Starting PowerPaste: Direction={Direction}, Explode={Explode}, ClipCount={ClipCount}",
                direction, explode, clips.Count);

            _sequence.Clear();
            _pasteCount = 0;
            _loadContent = loadContent;

            // A single clip without Explode pastes once and stops, even with Loop on
            _isSingleClip = clips.Count == 1 && !explode;

            Direction = direction;

            // A sequence can run to the end of a large collection, so a loader gets each clip's content only when it
            // comes up. Without one the clips are already loaded.
            if (loadContent != null)
                _sequence.AddRange(clips.Select(p => new PowerPasteStep(p, false, false)));
            else if (explode)
            {
                foreach (var clip in clips)
                    _sequence.AddRange(OrderedForDirection(ExplodeClip(clip, preferences)));
            }
            else
                _sequence.AddRange(clips.Select(p => new PowerPasteStep(p, false)));

            CurrentPosition = direction == PowerPasteDirection.Down
                ? 0
                : _sequence.Count - 1;

            preferences.PowerPasteLastDirection = direction.ToString();

            var oldState = State;
            State = PowerPasteState.Active;
            _status.IsActive = true;

            // Restarting replaces the sequence without giving up the clipboard
            if (oldState != PowerPasteState.Active)
                OnUiThread(Hold);

            StateChanged?.Invoke(this, new PowerPasteStateChangedEventArgs
            {
                OldState = oldState,
                NewState = State,
                Direction = Direction,
                TotalCount = _sequence.Count,
            });

            MoveToCurrentPosition();

            _logger.LogInformation("PowerPaste started: Position={Position}/{TotalCount}", CurrentPosition + 1, _sequence.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ToggleAsync(Func<PowerPasteDirection, IReadOnlyList<Clip>> getClips, Func<Clip, Task>? loadContent = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(getClips);

        if (State == PowerPasteState.Active)
        {
            _logger.LogInformation("PowerPaste stopped via toggle");
            Stop();

            return;
        }

        await StartWithClipsAsync(NextDirection, getClips, loadContent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ToggleAsync(PowerPasteDirection direction, Func<PowerPasteDirection, IReadOnlyList<Clip>> getClips, Func<Clip, Task>? loadContent = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(getClips);

        if (State == PowerPasteState.Active && Direction == direction)
        {
            _logger.LogInformation("PowerPaste {Direction} stopped via its own button", direction);
            Stop();

            return;
        }

        await StartWithClipsAsync(direction, getClips, loadContent, cancellationToken);
    }

    private async Task StartWithClipsAsync(PowerPasteDirection direction, Func<PowerPasteDirection, IReadOnlyList<Clip>> getClips, Func<Clip, Task>? loadContent,
        CancellationToken cancellationToken)
    {
        var clips = getClips(direction);
        if (clips.Count == 0)
        {
            _logger.LogDebug("PowerPaste ignored: no clips selected");

            return;
        }

        await StartAsync(clips, direction, loadContent, cancellationToken);
    }

    /// <summary>
    /// The direction the button starts in: the one saved, which stopping without a paste has flipped. ClipMate 7 starts
    /// going up until a direction was used.
    /// </summary>
    private PowerPasteDirection NextDirection =>
        Enum.TryParse<PowerPasteDirection>(Preferences.PowerPasteLastDirection, out var saved)
            ? saved
            : PowerPasteDirection.Up;

    /// <inheritdoc />
    public bool MoveTo(Guid clipId)
    {
        _lock.Wait();
        try
        {
            if (State != PowerPasteState.Active)
                return false;

            // Explode pieces are laid out in paste order for the direction, so this is the clip's first piece either way
            var position = Direction == PowerPasteDirection.Down
                ? _sequence.FindIndex(p => p.Clip.Id == clipId)
                : _sequence.FindLastIndex(p => p.Clip.Id == clipId);

            // A clip outside the sequence sends it back to its start, and the list back to that clip
            var isInSequence = position >= 0;
            if (!isInSequence)
            {
                position = Direction == PowerPasteDirection.Down
                    ? 0
                    : _sequence.Count - 1;
            }

            if (position != CurrentPosition || !isInSequence)
            {
                CurrentPosition = position;
                _logger.LogInformation("PowerPaste moved to {ClipId}: Position={Position}/{TotalCount}", _sequence[position].Clip.Id, CurrentPosition + 1, _sequence.Count);
                MoveToCurrentPosition();
            }

            return isInSequence;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Stop() => Stop(true);

    /// <inheritdoc />
    public void Stop(bool keepWaitingClip)
    {
        _lock.Wait();
        try
        {
            StopInternal(keepWaitingClip);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// The waiting clip was pasted: the sequence moves on to the next clip, or ends.
    /// </summary>
    private async Task AdvanceAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (State != PowerPasteState.Active)
                return;

            _pasteCount++;
            CurrentPosition += Direction == PowerPasteDirection.Down ? 1 : -1;

            _logger.LogDebug("PowerPaste advanced: Position={Position}/{TotalCount}", CurrentPosition + 1, _sequence.Count);

            var isComplete = Direction == PowerPasteDirection.Down
                ? CurrentPosition >= _sequence.Count
                : CurrentPosition < 0;

            if (isComplete)
            {
                if (!Preferences.PowerPasteLoop || _isSingleClip)
                {
                    _logger.LogInformation("PowerPaste sequence complete");
                    StopInternal(true);

                    return;
                }

                CurrentPosition = Direction == PowerPasteDirection.Down
                    ? 0
                    : _sequence.Count - 1;

                _logger.LogInformation("PowerPaste looping: Position reset to {Position}", CurrentPosition + 1);

                // Double beep to indicate loop, without holding up the next paste
                _ = PlayLoopSoundAsync();
            }

            MoveToCurrentPosition();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Tells listeners the waiting clip changed and offers it, once its content is loaded. Call with the lock held.
    /// </summary>
    private void MoveToCurrentPosition()
    {
        var item = _sequence[CurrentPosition];
        if (!item.IsReady)
        {
            _ = PrepareAndMoveAsync(item);

            return;
        }

        var position = new PowerPastePositionChangedEventArgs
        {
            Position = CurrentPosition,
            TotalCount = _sequence.Count,
            CurrentClip = item.Clip,
            IsExplodePiece = item.IsExplodePiece,
        };

        // The offer comes first. Listeners move the clip list's selection, and rendering it can hold the UI thread for
        // over half a second; until the clip is offered, a paste gets the previous clip again and goes unnoticed.
        OnUiThread(() =>
        {
            Offer(item);
            PositionChanged?.Invoke(this, position);
        });
    }

    /// <summary>
    /// Loads a step's content, explodes it when the setting is on, and moves on to it if the sequence is still there.
    /// </summary>
    private async Task PrepareAndMoveAsync(PowerPasteStep item)
    {
        try
        {
            if (_loadContent is { } loadContent)
                await loadContent(item.Clip);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PowerPaste could not load clip {ClipId}", item.Clip.Id);
        }

        await _lock.WaitAsync();
        try
        {
            var index = _sequence.IndexOf(item);
            if (State != PowerPasteState.Active || index < 0)
                return;

            var preferences = Preferences;
            var steps = preferences.PowerPasteExplode
                ? OrderedForDirection(ExplodeClip(item.Clip, preferences))
                : [item with { IsReady = true }];

            _sequence.RemoveAt(index);
            _sequence.InsertRange(index, steps);

            if (index == CurrentPosition)
            {
                // Going up, the sequence is walked backward, so the clip's first piece is its last step
                if (Direction == PowerPasteDirection.Up)
                    CurrentPosition = index + steps.Count - 1;

                MoveToCurrentPosition();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// A clip's Explode pieces always paste in reading order. Going up the sequence is walked backward, so they are
    /// laid out reversed.
    /// </summary>
    private List<PowerPasteStep> OrderedForDirection(List<PowerPasteStep> steps)
    {
        if (Direction == PowerPasteDirection.Up)
            steps.Reverse();

        return steps;
    }

    /// <summary>
    /// Stops the sequence. Call with the lock held.
    /// </summary>
    private void StopInternal(bool keepWaitingClip)
    {
        if (State == PowerPasteState.Inactive)
            return;

        _logger.LogInformation("Stopping PowerPaste after {PasteCount} paste(s)", _pasteCount);

        // Only a sequence that was pasted from is complete. One that was not was probably going the wrong way, so the
        // button starts the other way next time.
        if (_pasteCount > 0)
            _ = PlayCompletionSoundAsync();
        else
        {
            Preferences.PowerPasteLastDirection = (Direction == PowerPasteDirection.Down
                ? PowerPasteDirection.Up
                : PowerPasteDirection.Down).ToString();
        }

        var oldState = State;
        State = PowerPasteState.Inactive;
        _status.IsActive = false;
        _sequence.Clear();
        CurrentPosition = -1;

        // At once when on the UI thread: when another program took the clipboard, capture must be back on before its
        // clipboard update is handled, or its copy is missed
        UiThread.Run(() => Release(keepWaitingClip));

        StateChanged?.Invoke(this, new PowerPasteStateChangedEventArgs
        {
            OldState = oldState,
            NewState = State,
            Direction = Direction,
            TotalCount = 0,
        });
    }

    /// <summary>
    /// Holds capture while Power Paste has the clipboard: every offer is a clipboard change, and capture would read
    /// each one and save every clip of the sequence again.
    /// </summary>
    private void Hold()
    {
        // Power Paste stopped before it took the clipboard
        if (_isHolding || State != PowerPasteState.Active)
            return;

        _isHolding = true;
        _lastPasteAt = DateTimeOffset.MinValue;
        _backgroundReads.Clear();
        _endlessReaders.Clear();
        _clipboardService.SuspendCapture();
    }

    /// <summary>
    /// Offers the waiting clip with the active template applied to its text. The template's #SEQUENCE# number is used
    /// up only when the clip is pasted. An Explode piece is plain text only.
    /// </summary>
    private void Offer(PowerPasteStep item)
    {
        if (!_isHolding)
        {
            _logger.LogDebug("PowerPaste stopped before clip {ClipId} came up; not offering it", item.Clip.Id);

            return;
        }

        CancelAdvance();
        _generation++;
        _reOfferPending = false;
        _recentReOffers.Clear();

        // Only the text changes; every other Format of the clip is offered as it is
        var clip = item.Clip;
        var templatedText = _templateService.PreviewTemplateText(clip);
        _waitingClipIsTemplated = templatedText != null;
        _waitingClip = templatedText == null
            ? clip
            : clip.WithText(templatedText);

        IReadOnlyList<ClipboardFormatData> formats;
        if (item.IsExplodePiece)
        {
            formats = [new ClipboardFormatData((uint)Formats.UnicodeText.Code, DataObjectFormats.EncodeUnicodeText(_waitingClip.TextContent ?? string.Empty))];
            _currentClipWriter.ReportCurrent(_waitingClip);
        }
        else
            formats = _currentClipWriter.Prepare(_waitingClip);

        if (formats.Count == 0)
        {
            _logger.LogWarning("PowerPaste clip {ClipId} has nothing to put on the clipboard", clip.Id);

            return;
        }

        _clipOfferedAt = _timeProvider.GetUtcNow();
        if (_clipboard.Offer(formats))
            _logger.LogInformation("PowerPaste offered clip {ClipId} in {FormatCount} format(s)", clip.Id, formats.Count);
        else
            _logger.LogWarning("PowerPaste could not open the clipboard to offer clip {ClipId}", clip.Id);
    }

    /// <summary>
    /// Ends Power Paste's hold on the clipboard. With <paramref name="keepWaitingClip" /> the waiting clip is put back
    /// as a normal copy with all its Formats; without it nothing more is written (another application took the
    /// clipboard, or a picked clip is about to be copied).
    /// </summary>
    private void Release(bool keepWaitingClip)
    {
        if (!_isHolding)
            return;

        CancelAdvance();
        _generation++;
        _isHolding = false;

        var clip = _waitingClip;
        _waitingClip = null;

        _clipboard.Release();
        _clipboardService.ResumeCapture();

        if (keepWaitingClip && clip != null)
            _ = PutBackAsync(clip);
    }

    private async Task PutBackAsync(Clip clip)
    {
        // A normal copy, marked as ClipMate's own so it is not captured again; the clip stays in the list untouched
        var copy = clip.WithText(clip.TextContent);

        try
        {
            await Task.Yield();

            // Power Paste was started again meanwhile and holds the clipboard
            if (State == PowerPasteState.Active)
                return;

            await _currentClipWriter.PutOnClipboardAsync(copy);
            _logger.LogInformation("PowerPaste left the waiting clip {ClipId} on the clipboard", copy.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PowerPaste could not leave the waiting clip {ClipId} on the clipboard", copy.Id);
        }
    }

    private void OnClipboardRead(object? sender, PowerPasteClipboardReadEventArgs e)
    {
        if (!_isHolding || _waitingClip == null)
        {
            _logger.LogDebug("PowerPaste ignoring a clipboard read by {ReaderProcess}: no clip is waiting", e.ReaderName);

            return;
        }

        var now = _timeProvider.GetUtcNow();
        var isShielded = Preferences.PowerPasteShield;
        if (!IsPaste(e, now, isShielded))
        {
            // Clipboard history, another clipboard manager, ClipMate itself: it got the data, but the user has not pasted
            _logger.LogDebug("PowerPaste clipboard read by {ReaderProcess} is not a paste", e.ReaderName);
            if (IsHiddenFrom(e) || IsEndlessReader(e, now))
            {
                e.IsRefused = true;

                return;
            }

            ScheduleReOffer();

            return;
        }

        if (isShielded && now - _lastPasteAt < _minPasteInterval)
        {
            _logger.LogDebug("PowerPaste ignoring clipboard read by {ReaderProcess} {ElapsedMs}ms after the last paste - too soon to be a new paste",
                e.ReaderName, (now - _lastPasteAt).TotalMilliseconds);

            ScheduleReOffer();

            return;
        }

        _lastPasteAt = now;
        _logger.LogInformation("PowerPaste clip {ClipId} pasted into {ReaderProcess}", _waitingClip.Id, e.ReaderName);

        // The next clip gets the next number
        if (_waitingClipIsTemplated)
            _templateService.AdvanceSequence();

        AdvanceAfterDelay();
    }

    /// <summary>
    /// With PowerPaste Shield off, every read by another program is a paste, as in ClipMate 7, where background readers
    /// can "fast forward" the sequence. With it on, a window of the application in the foreground always pastes, a named
    /// background program never does, and a read that cannot be tied to a process pastes when it follows the user's
    /// input: see <see cref="_inputBeforePaste" /> and <see cref="_inputAfterOffer" />.
    /// </summary>
    private bool IsPaste(PowerPasteClipboardReadEventArgs read, DateTimeOffset now, bool isShielded)
    {
        if (read.IsByClipMate)
            return false;

        if (!isShielded)
            return true;

        if (read.IsClipMateInForeground)
            return false;

        if (read.IsByForegroundApplication)
            return true;

        return read.MayBeForForegroundApplication
               && read.SinceUserInput <= _inputBeforePaste
               && now - read.SinceUserInput >= _clipOfferedAt + _inputAfterOffer;
    }

    /// <summary>
    /// True when the user chose to hide clips from programs in the background. ClipMate needs its own reads, and a read
    /// without a process may be a paste.
    /// </summary>
    private bool IsHiddenFrom(PowerPasteClipboardReadEventArgs read) =>
        Preferences.PowerPasteHideFromOtherPrograms && !read.IsByClipMate && !read.MayBeForForegroundApplication;

    /// <summary>
    /// True for a named background program that has read <see cref="EndlessReaderReads" /> times within
    /// <see cref="_endlessReaderWindow" />. ClipMate needs its own reads, and a read without a process may be a paste.
    /// </summary>
    private bool IsEndlessReader(PowerPasteClipboardReadEventArgs read, DateTimeOffset now)
    {
        if (read.IsByClipMate || read.MayBeForForegroundApplication)
            return false;

        if (_endlessReaders.Contains(read.ReaderName))
            return true;

        if (!_backgroundReads.TryGetValue(read.ReaderName, out var reads))
            _backgroundReads[read.ReaderName] = reads = new Queue<DateTimeOffset>();

        reads.Enqueue(now);
        while (now - reads.Peek() > _endlessReaderWindow)
            reads.Dequeue();

        if (reads.Count < EndlessReaderReads)
            return false;

        _logger.LogInformation("PowerPaste refusing {ReaderProcess}: it reads the clipboard after every offer", read.ReaderName);
        _endlessReaders.Add(read.ReaderName);

        return true;
    }

    private void OnClipboardLost(object? sender, EventArgs e)
    {
        _logger.LogInformation("PowerPaste clipboard taken by another program - stopping PowerPaste");
        Stop(false);
    }

    /// <summary>
    /// Moves the sequence on after the PowerPasteDelay setting, outside the clipboard message that reported the paste.
    /// </summary>
    private void AdvanceAfterDelay()
    {
        CancelAdvance();
        var delay = _advanceDelay = new CancellationTokenSource();
        var wait = TimeSpan.FromMilliseconds(Math.Max(0, Preferences.PowerPasteDelay));

        _ = Task.Delay(wait, _timeProvider, delay.Token)
            .ContinueWith(_ => OnUiThread(() =>
                {
                    if (!delay.IsCancellationRequested)
                        _ = RunAdvanceAsync();
                }),
                CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private async Task RunAdvanceAsync()
    {
        try
        {
            await AdvanceAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error advancing PowerPaste sequence after a paste");
        }
    }

    private void CancelAdvance()
    {
        _advanceDelay?.Cancel();
        _advanceDelay?.Dispose();
        _advanceDelay = null;
    }

    /// <summary>
    /// Offers the waiting clip again once the reader is done with the clipboard: after <see cref="_reOfferSettleTime" />,
    /// or later when it was already offered again <see cref="QuickReOffersPerInterval" /> times within
    /// <see cref="_reOfferInterval" />.
    /// </summary>
    private void ScheduleReOffer()
    {
        if (_reOfferPending)
            return;

        _reOfferPending = true;
        var generation = _generation;
        var wait = _reOfferSettleTime;
        if (_recentReOffers.Count == QuickReOffersPerInterval)
        {
            var throttled = _recentReOffers.Peek() + _reOfferInterval - _timeProvider.GetUtcNow();
            if (throttled > wait)
                wait = throttled;
        }

        _ = Task.Delay(wait, _timeProvider)
            .ContinueWith(_ => OnUiThread(() => ReOffer(generation)),
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void ReOffer(int generation)
    {
        // The next clip was offered, or Power Paste stopped, meanwhile
        if (generation != _generation)
            return;

        _reOfferPending = false;
        _recentReOffers.Enqueue(_timeProvider.GetUtcNow());
        if (_recentReOffers.Count > QuickReOffersPerInterval)
            _recentReOffers.Dequeue();

        if (_clipboard.OfferAgain())
            _logger.LogDebug("PowerPaste offered clip {ClipId} again after a read that was not a paste", _waitingClip?.Id);
        else
            _logger.LogWarning("PowerPaste could not offer clip {ClipId} again", _waitingClip?.Id);
    }

    /// <summary>
    /// Queues clipboard work on the UI thread, behind any already queued, so it never runs inside the clipboard
    /// message that triggered it and offers stay in order. Each step checks it is still wanted when it runs.
    /// </summary>
    private static void OnUiThread(Action action) => UiThread.Queue(action);

    /// <summary>
    /// Splits a clip's text into Explode pieces at any of the delimiter characters. With "include delimiter" each piece
    /// keeps the delimiter that ended it.
    /// </summary>
    private List<PowerPasteStep> ExplodeClip(Clip clip, PreferencesConfiguration preferences)
    {
        var text = clip.TextContent;
        if (string.IsNullOrWhiteSpace(text))
            return [new PowerPasteStep(clip, false)];

        // The delimiter setting writes line breaks and tabs as \n, \t and \r
        var delimiters = preferences.PowerPasteDelimiter
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\r", "\r");

        var pieces = new List<PowerPasteStep>();
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            var isEnd = i == text.Length;
            if (!isEnd && !delimiters.Contains(text[i]))
                continue;

            var piece = text[start..i];
            start = i + 1;

            if (preferences.PowerPasteTrim)
                piece = piece.Trim();

            if (piece.Length == 0)
                continue;

            if (preferences.PowerPasteIncludeDelimiter && !isEnd)
                piece += text[i];

            // A piece keeps the clip's ID, so the clip list follows the sequence
            pieces.Add(new PowerPasteStep(new Clip
            {
                Id = clip.Id,
                TextContent = piece,
                Type = ClipType.Text,
                CapturedAt = clip.CapturedAt,
                SourceApplicationName = clip.SourceApplicationName,
                CollectionId = clip.CollectionId,
            }, true));
        }

        if (pieces.Count == 0)
            return [new PowerPasteStep(clip, false)];

        _logger.LogDebug("Exploded clip into {PieceCount} pieces", pieces.Count);

        return pieces;
    }

    /// <summary>
    /// Plays a beep sound when PowerPaste completes.
    /// </summary>
    private async Task PlayCompletionSoundAsync()
    {
        try
        {
            await _soundService.PlaySoundAsync(SoundEvent.PowerPasteComplete);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not play the PowerPaste complete sound");
        }
    }

    /// <summary>
    /// Plays the completion sound twice, 250 ms apart, when PowerPaste loops.
    /// </summary>
    private async Task PlayLoopSoundAsync()
    {
        await PlayCompletionSoundAsync();
        await Task.Delay(_loopBeepInterval, _timeProvider);
        await PlayCompletionSoundAsync();
    }
}
