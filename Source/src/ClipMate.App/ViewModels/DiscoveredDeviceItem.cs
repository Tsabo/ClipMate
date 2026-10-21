namespace ClipMate.App.ViewModels;

/// <summary>
/// A Discovered Device as listed on the Devices page.
/// </summary>
/// <param name="DeviceName">The Device Name it announces.</param>
/// <param name="ShortDeviceId">The short form of its Device ID.</param>
/// <param name="DeviceId">The full, grouped Device ID (shown as a tooltip).</param>
public sealed record DiscoveredDeviceItem(string DeviceName, string ShortDeviceId, string DeviceId);
