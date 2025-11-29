using System.Text.RegularExpressions;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing PowerPaste sequential automation.
/// PowerPaste automatically pastes clips one-by-one in sequence as the user performs paste operations.
/// </summary>
public class PowerPasteService : IPowerPasteService
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<PowerPasteService> _logger;
    private readonly Platform.ISoundService _soundService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private PowerPasteState _state = PowerPasteState.Inactive;
    private PowerPasteDirection _direction = PowerPasteDirection.Down;
    private List<Clip> _sequence = [];
    private int _currentPosition = -1;
    private bool _explodeMode;

    public PowerPasteService(
        IConfigurationService configurationService,
        Platform.ISoundService soundService,
        ILogger<PowerPasteService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public PowerPasteState State => _state;

    /// <inheritdoc />
    public PowerPasteDirection Direction => _direction;

    /// <inheritdoc />
    public int CurrentPosition => _currentPosition;

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
            _explodeMode = explodeMode;

            if (explodeMode)
            {
                // Explode mode: split clips into fragments
                foreach (var clip in clips)
                {
                    var fragments = ExplodeClip(clip, config);
                    _sequence.AddRange(fragments);
                }
            }
            else
            {
                // Normal mode: use clips as-is
                _sequence.AddRange(clips);
            }

            if (_sequence.Count == 0)
            {
                _logger.LogWarning("PowerPaste sequence is empty after processing");
                return;
            }

            // Initialize position based on direction
            _direction = direction;
            _currentPosition = direction == PowerPasteDirection.Down ? 0 : _sequence.Count - 1;

            // Update state
            var oldState = _state;
            _state = PowerPasteState.Active;

            // Raise events
            StateChanged?.Invoke(this, new PowerPasteStateChangedEventArgs
            {
                OldState = oldState,
                NewState = _state,
                Direction = _direction,
                TotalCount = _sequence.Count
            });

            PositionChanged?.Invoke(this, new PowerPastePositionChangedEventArgs
            {
                Position = _currentPosition,
                TotalCount = _sequence.Count,
                CurrentClip = GetCurrentClip(),
                IsComplete = false
            });

            _logger.LogInformation("PowerPaste started: Position={Position}/{TotalCount}", _currentPosition + 1, _sequence.Count);
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
            if (_state != PowerPasteState.Active)
            {
                _logger.LogWarning("AdvanceToNext called but PowerPaste is not active");
                return;
            }

            var config = _configurationService.Configuration.Preferences;

            // Move to next position
            if (_direction == PowerPasteDirection.Down)
                _currentPosition++;
            else
                _currentPosition--;

            _logger.LogDebug("PowerPaste advanced: Position={Position}/{TotalCount}", _currentPosition + 1, _sequence.Count);

            // Check if we've reached the end
            var isComplete = _direction == PowerPasteDirection.Down
                ? _currentPosition >= _sequence.Count
                : _currentPosition < 0;

            if (isComplete)
            {
                if (config.PowerPasteLoop)
                {
                    // Loop back to the beginning
                    _currentPosition = _direction == PowerPasteDirection.Down ? 0 : _sequence.Count - 1;
                    _logger.LogInformation("PowerPaste looping: Position reset to {Position}", _currentPosition + 1);

                    // Play double beep to indicate loop
                    await PlayLoopSoundAsync();
                }
                else
                {
                    // Sequence complete
                    _logger.LogInformation("PowerPaste sequence complete");
                    await PlayCompletionSoundAsync();

                    Stop();
                    return;
                }
            }

            // Raise position changed event
            PositionChanged?.Invoke(this, new PowerPastePositionChangedEventArgs
            {
                Position = _currentPosition,
                TotalCount = _sequence.Count,
                CurrentClip = GetCurrentClip(),
                IsComplete = isComplete && !config.PowerPasteLoop
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        _lock.Wait();
        try
        {
            if (_state == PowerPasteState.Inactive)
                return;

            _logger.LogInformation("Stopping PowerPaste");

            var oldState = _state;
            _state = PowerPasteState.Inactive;
            _sequence.Clear();
            _currentPosition = -1;

            StateChanged?.Invoke(this, new PowerPasteStateChangedEventArgs
            {
                OldState = oldState,
                NewState = _state,
                Direction = _direction,
                TotalCount = 0
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public Clip? GetCurrentClip()
    {
        if (_state != PowerPasteState.Active || _currentPosition < 0 || _currentPosition >= _sequence.Count)
            return null;

        return _sequence[_currentPosition];
    }

    /// <summary>
    /// Explodes a clip into fragments based on delimiter configuration.
    /// </summary>
    private List<Clip> ExplodeClip(Clip clip, PreferencesConfiguration config)
    {
        var fragments = new List<Clip>();

        if (string.IsNullOrWhiteSpace(clip.TextContent))
        {
            fragments.Add(clip);
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
                CollectionId = clip.CollectionId
            };

            fragments.Add(fragment);
        }

        // If no fragments were created (all empty), return original clip
        if (fragments.Count == 0)
            fragments.Add(clip);

        _logger.LogDebug("Exploded clip into {FragmentCount} fragments", fragments.Count);
        return fragments;
    }

    /// <summary>
    /// Plays a beep sound when PowerPaste completes.
    /// </summary>
    private Task PlayCompletionSoundAsync()
    {
        // TODO: Implement PowerPaste completion sound with new Platform.ISoundService
        // Need to add PowerPasteComplete to SoundEvent enum and create sound file
        return Task.CompletedTask;
    }

    /// <summary>
    /// Plays a double beep sound when PowerPaste loops.
    /// </summary>
    private Task PlayLoopSoundAsync()
    {
        // TODO: Implement PowerPaste loop sound with new Platform.ISoundService
        // Need to add PowerPasteLoop to SoundEvent enum and create sound file
        return Task.CompletedTask;
    }
}
