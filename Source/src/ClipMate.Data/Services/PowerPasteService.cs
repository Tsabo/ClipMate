using System.Text.RegularExpressions;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing PowerPaste sequential automation.
/// PowerPaste automatically pastes clips one-by-one in sequence as the user performs paste operations.
/// </summary>
public class PowerPasteService : IPowerPasteService
{
    // ClipMate 7 beeps twice, this far apart, when a looping sequence starts over
    private static readonly TimeSpan _loopBeepInterval = TimeSpan.FromMilliseconds(250);

    private readonly IConfigurationService _configurationService;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<PowerPasteService> _logger;
    private readonly List<SequenceItem> _sequence = [];
    private readonly ISoundService _soundService;
    private readonly PowerPasteStatus _status;
    private readonly TimeProvider _timeProvider;
    private bool _isSingleClip;
    private int _pasteCount;

    public PowerPasteService(IConfigurationService configurationService,
        ISoundService soundService,
        ILogger<PowerPasteService> logger,
        PowerPasteStatus? status = null,
        TimeProvider? timeProvider = null)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _status = status ?? new PowerPasteStatus();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public PowerPasteState State { get; private set; } = PowerPasteState.Inactive;

    /// <inheritdoc />
    public PowerPasteDirection Direction { get; private set; } = PowerPasteDirection.Down;

    /// <inheritdoc />
    public int CurrentPosition { get; private set; } = -1;

    /// <inheritdoc />
    public int TotalCount => _sequence.Count;

    /// <inheritdoc />
    public event EventHandler<PowerPasteStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<PowerPastePositionChangedEventArgs>? PositionChanged;

    /// <inheritdoc />
    public async Task StartAsync(IReadOnlyList<Clip> clips, PowerPasteDirection direction, bool explodeMode = false, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (clips == null || clips.Count == 0)
                throw new ArgumentException("Clips collection cannot be null or empty", nameof(clips));

            _logger.LogInformation("Starting PowerPaste: Direction={Direction}, ExplodeMode={ExplodeMode}, ClipCount={ClipCount}",
                direction, explodeMode, clips.Count);

            var config = _configurationService.Configuration.Preferences;

            // Build the sequence
            _sequence.Clear();
            _pasteCount = 0;

            // A single clip without Explode pastes once and stops, even with Loop on
            _isSingleClip = clips.Count == 1 && !explodeMode;

            if (explodeMode)
            {
                // Explode mode: split clips into fragments
                foreach (var clip in clips)
                    _sequence.AddRange(ExplodeClip(clip, config));
            }
            else
            {
                // Normal mode: use clips as-is
                _sequence.AddRange(clips.Select(p => new SequenceItem(p, false)));
            }

            if (_sequence.Count == 0)
            {
                _logger.LogWarning("PowerPaste sequence is empty after processing");

                return;
            }

            // Initialize position based on direction
            Direction = direction;
            CurrentPosition = direction == PowerPasteDirection.Down
                ? 0
                : _sequence.Count - 1;

            // Update state
            var oldState = State;
            State = PowerPasteState.Active;
            _status.IsActive = true;

            // Raise events
            StateChanged?.Invoke(this, new PowerPasteStateChangedEventArgs
            {
                OldState = oldState,
                NewState = State,
                Direction = Direction,
                TotalCount = _sequence.Count,
            });

            RaisePositionChanged();

            _logger.LogInformation("PowerPaste started: Position={Position}/{TotalCount}", CurrentPosition + 1, _sequence.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task AdvanceToNextAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (State != PowerPasteState.Active)
            {
                _logger.LogWarning("AdvanceToNext called but PowerPaste is not active");

                return;
            }

            var config = _configurationService.Configuration.Preferences;
            _pasteCount++;

            // Move to next position
            if (Direction == PowerPasteDirection.Down)
                CurrentPosition++;
            else
                CurrentPosition--;

            _logger.LogDebug("PowerPaste advanced: Position={Position}/{TotalCount}", CurrentPosition + 1, _sequence.Count);

            // Check if we've reached the end
            var isComplete = Direction == PowerPasteDirection.Down
                ? CurrentPosition >= _sequence.Count
                : CurrentPosition < 0;

            if (isComplete)
            {
                if (config.PowerPasteLoop && !_isSingleClip)
                {
                    // Loop back to the beginning
                    CurrentPosition = Direction == PowerPasteDirection.Down
                        ? 0
                        : _sequence.Count - 1;

                    _logger.LogInformation("PowerPaste looping: Position reset to {Position}", CurrentPosition + 1);

                    // Double beep to indicate loop, without holding up the next paste
                    _ = PlayLoopSoundAsync();
                }
                else
                {
                    // Sequence complete
                    _logger.LogInformation("PowerPaste sequence complete");

                    StopInternal(true);

                    return;
                }
            }

            RaisePositionChanged();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public bool MoveTo(Guid clipId)
    {
        _lock.Wait();
        try
        {
            if (State != PowerPasteState.Active)
                return false;

            var position = Direction == PowerPasteDirection.Down
                ? _sequence.FindIndex(p => p.Clip.Id == clipId)
                : _sequence.FindLastIndex(p => p.Clip.Id == clipId);

            if (position < 0)
                return false;

            if (position != CurrentPosition)
            {
                CurrentPosition = position;
                _logger.LogInformation("PowerPaste moved to clip {ClipId}: Position={Position}/{TotalCount}", clipId, CurrentPosition + 1, _sequence.Count);
                RaisePositionChanged();
            }

            return true;
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

    /// <inheritdoc />
    public Clip? GetCurrentClip()
    {
        if (State != PowerPasteState.Active || CurrentPosition < 0 || CurrentPosition >= _sequence.Count)
            return null;

        return _sequence[CurrentPosition].Clip;
    }

    private void RaisePositionChanged() =>
        PositionChanged?.Invoke(this, new PowerPastePositionChangedEventArgs
        {
            Position = CurrentPosition,
            TotalCount = _sequence.Count,
            CurrentClip = _sequence[CurrentPosition].Clip,
            IsExplodePiece = _sequence[CurrentPosition].IsExplodePiece,
        });

    /// <summary>
    /// Internal stop method that doesn't acquire the lock.
    /// Should only be called when the lock is already held.
    /// </summary>
    private void StopInternal(bool keepWaitingClip)
    {
        if (State == PowerPasteState.Inactive)
            return;

        _logger.LogInformation("Stopping PowerPaste after {PasteCount} paste(s)", _pasteCount);

        // Only a sequence that was pasted from is complete
        if (_pasteCount > 0)
            _ = PlayCompletionSoundAsync();

        var oldState = State;
        State = PowerPasteState.Inactive;
        _status.IsActive = false;
        _sequence.Clear();
        CurrentPosition = -1;

        StateChanged?.Invoke(this, new PowerPasteStateChangedEventArgs
        {
            OldState = oldState,
            NewState = State,
            Direction = Direction,
            TotalCount = 0,
            KeepWaitingClip = keepWaitingClip,
        });
    }

    /// <summary>
    /// Explodes a clip into fragments based on delimiter configuration.
    /// </summary>
    private List<SequenceItem> ExplodeClip(Clip clip, PreferencesConfiguration config)
    {
        var fragments = new List<SequenceItem>();

        if (string.IsNullOrWhiteSpace(clip.TextContent))
        {
            fragments.Add(new SequenceItem(clip, false));

            return fragments;
        }

        // Parse delimiter string (supports escape sequences like \n, \t)
        var delimiter = config.PowerPasteDelimiter
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\r", "\r");

        // Split by any character in the delimiter string
        var parts = Regex.Split(clip.TextContent, $"[{Regex.Escape(delimiter)}]");

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            // Apply trim if configured
            if (config.PowerPasteTrim)
                part = part.Trim();

            // Skip empty fragments
            if (string.IsNullOrEmpty(part))
                continue;

            // Include delimiter if configured (append to fragment except last one)
            if (config.PowerPasteIncludeDelimiter && i < parts.Length - 1)
            {
                // Find the delimiter that was used to split
                var originalIndex = clip.TextContent.IndexOf(part, StringComparison.Ordinal);
                if (originalIndex >= 0 && originalIndex + part.Length < clip.TextContent.Length)
                {
                    var delimiterChar = clip.TextContent[originalIndex + part.Length];
                    if (delimiter.Contains(delimiterChar))
                        part += delimiterChar;
                }
            }

            // Create fragment clip (reuse same ID, just different text content)
            var fragment = new Clip
            {
                Id = clip.Id,
                TextContent = part,
                Type = ClipType.Text,
                CapturedAt = clip.CapturedAt,
                SourceApplicationName = clip.SourceApplicationName,
                CollectionId = clip.CollectionId,
            };

            fragments.Add(new SequenceItem(fragment, true));
        }

        // If no fragments were created (all empty), return original clip
        if (fragments.Count == 0)
            fragments.Add(new SequenceItem(clip, false));

        _logger.LogDebug("Exploded clip into {FragmentCount} fragments", fragments.Count);

        return fragments;
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

    /// <summary>
    /// One step of the sequence: a clip, or one piece of an exploded clip.
    /// </summary>
    private sealed record SequenceItem(Clip Clip, bool IsExplodePiece);
}
