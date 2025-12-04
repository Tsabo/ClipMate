using System.IO;
using System.Media;
using ClipMate.Core.Models;
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
        var sound = _configurationService.Configuration.Preferences.Sound;

        return soundEvent switch
        {
            SoundEvent.ClipboardUpdate => sound.ClipboardUpdate != SoundMode.Off,
            SoundEvent.Append => sound.Append != SoundMode.Off,
            SoundEvent.Erase => sound.Erase != SoundMode.Off,
            SoundEvent.Filter => sound.Filter != SoundMode.Off,
            SoundEvent.Ignore => sound.Ignore != SoundMode.Off,
            SoundEvent.PowerPasteComplete => sound.PowerPasteComplete != SoundMode.Off,
            var _ => false,
        };
    }

    /// <summary>
    /// Gets the file path for the sound file associated with the specified event.
    /// Checks custom sound path first, then falls back to default sound file.
    /// </summary>
    private string GetSoundFilePath(SoundEvent soundEvent)
    {
        var sound = _configurationService.Configuration.Preferences.Sound;

        // Try to get custom sound path first
        var customPath = soundEvent switch
        {
            SoundEvent.ClipboardUpdate => sound.CustomClipboardUpdate,
            SoundEvent.Append => sound.CustomAppend,
            SoundEvent.Erase => sound.CustomErase,
            SoundEvent.Filter => sound.CustomFilter,
            SoundEvent.Ignore => sound.CustomIgnore,
            SoundEvent.PowerPasteComplete => sound.CustomPowerPasteComplete,
            var _ => null,
        };

        // If custom path is specified and file exists, use it
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return customPath;

        // Fall back to default sound file
        var defaultFileName = soundEvent switch
        {
            SoundEvent.ClipboardUpdate => "clipboard-update.wav",
            SoundEvent.Append => "append.wav",
            SoundEvent.Erase => "erase.wav",
            SoundEvent.Filter => "filter.wav",
            SoundEvent.Ignore => "ignore.wav",
            SoundEvent.PowerPasteComplete => "powerpaste-complete.wav",
            var _ => null,
        };

        return defaultFileName != null
            ? Path.Combine(_soundsDirectory, defaultFileName)
            : string.Empty;
    }
}
