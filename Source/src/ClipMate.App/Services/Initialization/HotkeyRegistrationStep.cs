using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Initialization step that registers all configured hotkeys with the system, and creates the hotkey navigation popup
/// (which registers for messenger events in its constructor) so it shows the clip from the first Scroll Next / Previous press.
/// </summary>
public class HotkeyRegistrationStep : IStartupInitializationStep
{
    private readonly HotkeyCoordinator _hotkeyCoordinator;
    private readonly ILogger<HotkeyRegistrationStep> _logger;

    // ReSharper disable once NotAccessedField.Local
    private readonly HotkeyNavigationPopup _navigationPopup;

    public HotkeyRegistrationStep(HotkeyCoordinator hotkeyCoordinator,
        HotkeyNavigationPopup navigationPopup,
        ILogger<HotkeyRegistrationStep> logger)
    {
        _hotkeyCoordinator = hotkeyCoordinator ?? throw new ArgumentNullException(nameof(hotkeyCoordinator));
        _navigationPopup = navigationPopup ?? throw new ArgumentNullException(nameof(navigationPopup));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "Hotkey Registration";

    public int Order => 50; // After configuration loading (20) and default data (30)

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering hotkeys...");

        try
        {
            _hotkeyCoordinator.RegisterAllHotkeys();
            _logger.LogInformation("Hotkeys registered successfully");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register hotkeys");
            throw;
        }
    }
}
