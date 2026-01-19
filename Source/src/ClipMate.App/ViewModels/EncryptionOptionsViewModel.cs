using ClipMate.Core.Models.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Encryption options page.
/// </summary>
public partial class EncryptionOptionsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _autoPromptForDecryption = true;

    [ObservableProperty]
    private int _defaultKeyRetentionMinutes = 1;

    [ObservableProperty]
    private bool _lockOnScreenLock = true;

    [ObservableProperty]
    private int _pbkdfIterations = 600_000;

    /// <summary>
    /// Loads settings from configuration.
    /// </summary>
    public void LoadFromConfiguration(EncryptionConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        PbkdfIterations = config.PbkdfIterations;
        DefaultKeyRetentionMinutes = config.DefaultKeyRetentionMinutes;
        LockOnScreenLock = config.LockOnScreenLock;
        AutoPromptForDecryption = config.AutoPromptForDecryption;
    }

    /// <summary>
    /// Saves settings to configuration.
    /// </summary>
    public void SaveToConfiguration(EncryptionConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.PbkdfIterations = PbkdfIterations;
        config.DefaultKeyRetentionMinutes = DefaultKeyRetentionMinutes;
        config.LockOnScreenLock = LockOnScreenLock;
        config.AutoPromptForDecryption = AutoPromptForDecryption;
    }
}
