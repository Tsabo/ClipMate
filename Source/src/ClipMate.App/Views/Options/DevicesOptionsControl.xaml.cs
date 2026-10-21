using ClipMate.App.ViewModels;

namespace ClipMate.App.Views.Options;

/// <summary>
/// Options page for peer networking: Enable networking, Device Name, Device ID and Discovered Devices.
/// </summary>
public partial class DevicesOptionsControl
{
    public DevicesOptionsControl()
    {
        InitializeComponent();

        // Follow the Discovered Devices only while the page is shown, so a closed Options dialog is not kept alive.
        Loaded += (_, _) => (DataContext as DevicesOptionsViewModel)?.StartWatchingDiscoveredDevices();
        Unloaded += (_, _) => (DataContext as DevicesOptionsViewModel)?.StopWatchingDiscoveredDevices();
    }
}
