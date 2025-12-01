using System.IO;
using System.Media;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for playing application sound notifications.
/// </summary>
public class SoundService : ISoundService
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<SoundService> _logger;
    private readonly string _soundsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundService" /> class.
    /// </summary>
    /// <param name="configurationService">Service for accessing application configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public SoundService(IConfigurationService configurationService,
        ILogger<SoundService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Sounds directory is in the application's Assets/Sounds folder
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _soundsDirectory = Path.Combine(appDirectory, "Assets", "Sounds");
    }

    /// <inheritdoc />
    public Task PlaySoundAsync(SoundEvent soundEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if sound is enabled in preferences
            if (!IsSoundEnabled(soundEvent))
                return Task.CompletedTask;

            // Get the sound file path for this event
            var soundFilePath = GetSoundFilePath(soundEvent);
            if (string.IsNullOrEmpty(soundFilePath) || !File.Exists(soundFilePath))
            {
                _logger.LogTrace("Sound file not found for event {SoundEvent}: {FilePath}", soundEvent, soundFilePath);
                return Task.CompletedTask;
            }

            // Play the sound asynchronously on a background thread to avoid blocking
            return Task.Run(() =>
            {
                try
                {
                    using var player = new SoundPlayer(soundFilePath);
                    player.PlaySync(); // PlaySync on background thread = async to caller
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to play sound for event {SoundEvent}", soundEvent);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in PlaySoundAsync for event {SoundEvent}", soundEvent);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Checks if sound is enabled for the specified event type in preferences.
    /// </summary>
    private bool IsSoundEnabled(SoundEvent soundEvent)
    {
        var preferences = _configurationService.Configuration.Preferences;

        return soundEvent switch
        {
            SoundEvent.ClipboardUpdate => preferences.BeepOnUpdate,
            SoundEvent.Append => preferences.BeepOnAppend,
            SoundEvent.Erase => preferences.BeepOnErase,
            SoundEvent.Filter => preferences.BeepOnFilter,
            SoundEvent.Ignore => preferences.BeepOnIgnore,
            var _ => false,
        };
    }

    /// <summary>
    /// Gets the file path for the sound file associated with the specified event.
    /// </summary>
    private string GetSoundFilePath(SoundEvent soundEvent)
    {
        var fileName = soundEvent switch
        {
            SoundEvent.ClipboardUpdate => "clipboard-update.wav",
            SoundEvent.Append => "append.wav",
            SoundEvent.Erase => "erase.wav",
            SoundEvent.Filter => "filter.wav",
            SoundEvent.Ignore => "ignore.wav",
            var _ => null,
        };

        return fileName != null
            ? Path.Combine(_soundsDirectory, fileName)
            : string.Empty;
    }
}
