namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Sound configuration for various clipboard and paste events.
/// </summary>
public class SoundConfiguration
{
    /// <summary>
    /// Gets or sets the sound mode for clipboard update events.
    /// </summary>
    public SoundMode ClipboardUpdate { get; set; } = SoundMode.Default;

    /// <summary>
    /// Gets or sets the sound mode for append events.
    /// </summary>
    public SoundMode Append { get; set; } = SoundMode.Default;

    /// <summary>
    /// Gets or sets the sound mode for erase events.
    /// </summary>
    public SoundMode Erase { get; set; } = SoundMode.Default;

    /// <summary>
    /// Gets or sets the sound mode for ignore events.
    /// </summary>
    public SoundMode Ignore { get; set; } = SoundMode.Default;

    /// <summary>
    /// Gets or sets the sound mode for filter events.
    /// </summary>
    public SoundMode Filter { get; set; } = SoundMode.Default;

    /// <summary>
    /// Gets or sets the sound mode for PowerPaste complete events.
    /// </summary>
    public SoundMode PowerPasteComplete { get; set; } = SoundMode.Default;

    /// <summary>
    /// Gets or sets the custom sound file path for clipboard update events.
    /// Null or empty indicates no custom sound is set.
    /// </summary>
    public string? CustomClipboardUpdate { get; set; }

    /// <summary>
    /// Gets or sets the custom sound file path for append events.
    /// Null or empty indicates no custom sound is set.
    /// </summary>
    public string? CustomAppend { get; set; }

    /// <summary>
    /// Gets or sets the custom sound file path for erase events.
    /// Null or empty indicates no custom sound is set.
    /// </summary>
    public string? CustomErase { get; set; }

    /// <summary>
    /// Gets or sets the custom sound file path for ignore events.
    /// Null or empty indicates no custom sound is set.
    /// </summary>
    public string? CustomIgnore { get; set; }

    /// <summary>
    /// Gets or sets the custom sound file path for filter events.
    /// Null or empty indicates no custom sound is set.
    /// </summary>
    public string? CustomFilter { get; set; }

    /// <summary>
    /// Gets or sets the custom sound file path for PowerPaste complete events.
    /// Null or empty indicates no custom sound is set.
    /// </summary>
    public string? CustomPowerPasteComplete { get; set; }
}
