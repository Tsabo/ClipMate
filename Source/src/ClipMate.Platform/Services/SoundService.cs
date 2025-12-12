using System.IO;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

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
                    using var audioFile = new AudioFileReader(soundFilePath);
                    using var outputDevice = new WaveOutEvent();
                    outputDevice.Init(audioFile);
                    outputDevice.Play();

                    // Wait for playback to complete
                    while (outputDevice.PlaybackState == PlaybackState.Playing)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Thread.Sleep(10);
                    }
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
    /// Supports both WAV and MP3 formats.
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

        // Fall back to default sound file (check both WAV and MP3)
        var baseFileName = soundEvent switch
        {
            SoundEvent.ClipboardUpdate => "clipboard-update",
            SoundEvent.Append => "append",
            SoundEvent.Erase => "erase",
            SoundEvent.Filter => "filter",
            SoundEvent.Ignore => "ignore",
            SoundEvent.PowerPasteComplete => "powerpaste-complete",
            var _ => null,
        };

        if (baseFileName == null)
            return string.Empty;

        // Check for WAV first, then MP3
        var wavPath = Path.Join(_soundsDirectory, $"{baseFileName}.wav");
        if (File.Exists(wavPath))
            return wavPath;

        var mp3Path = Path.Join(_soundsDirectory, $"{baseFileName}.mp3");
        if (File.Exists(mp3Path))
            return mp3Path;

        // Return WAV path even if it doesn't exist (for logging purposes)
        return wavPath;
    }
}
