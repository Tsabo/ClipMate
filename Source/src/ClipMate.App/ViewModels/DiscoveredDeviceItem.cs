using ClipMate.Core.Models.Networking;

namespace ClipMate.App.ViewModels;

/// <summary>
/// A Discovered Device as listed on the Devices page.
/// </summary>
/// <param name="DeviceName">The Device Name it announces.</param>
/// <param name="ShortDeviceId">The short form of its Device ID.</param>
/// <param name="DeviceId">The full, grouped Device ID (shown as a tooltip).</param>
/// <param name="StatusText">"Paired" if it is already a Peer, "Update required" if its protocol is incompatible, otherwise empty.</param>
/// <param name="CanPair">Whether <b>Pair</b> is available for it.</param>
/// <param name="Device">The Discovered Device.</param>
public sealed record DiscoveredDeviceItem(
    string DeviceName,
    string ShortDeviceId,
    string DeviceId,
    string StatusText,
    bool CanPair,
    DiscoveredDevice Device);
