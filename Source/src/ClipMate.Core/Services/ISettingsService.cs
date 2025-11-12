namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing application settings and preferences.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets a setting value by key.
    /// </summary>
    /// <typeparam name="T">The type of the setting value.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">Default value if setting not found.</param>
    /// <returns>The setting value.</returns>
    T GetSetting<T>(string key, T defaultValue);

    /// <summary>
    /// Sets a setting value by key.
    /// </summary>
    /// <typeparam name="T">The type of the setting value.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to store.</param>
    void SetSetting<T>(string key, T value);

    /// <summary>
    /// Saves all settings to persistent storage.
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads settings from persistent storage.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all settings to defaults.
    /// </summary>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}
