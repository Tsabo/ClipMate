using ClipMate.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WpfApplication = System.Windows.Application;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Ensures the hotkey service has a window handle before any hotkeys are registered.
/// </summary>
public class HotkeyInitializationStep : IStartupInitializationStep
{
    private readonly IHotkeyManager _hotkeyManager;
    private readonly ILogger<HotkeyInitializationStep> _logger;
    private readonly IServiceProvider _serviceProvider;
    private HotkeyWindow? _hotkeyWindow;

    public HotkeyInitializationStep(IHotkeyManager hotkeyManager,
        IServiceProvider serviceProvider,
        ILogger<HotkeyInitializationStep> logger)
    {
        _hotkeyManager = hotkeyManager ?? throw new ArgumentNullException(nameof(hotkeyManager));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "Hotkey Window Initialization";

    // Run before hotkey registration (50) but after config load
    public int Order => 40;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing hotkey window for WM_HOTKEY dispatch");

        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            // Create an invisible window dedicated to hotkey messages
            _hotkeyWindow = _serviceProvider.GetRequiredService<HotkeyWindow>();
            _hotkeyWindow.Show(); // Must be shown to create handle and pump messages
            _hotkeyManager.Initialize(_hotkeyWindow);
        });

        _logger.LogInformation("Hotkey window initialized");
        return Task.CompletedTask;
    }
}
