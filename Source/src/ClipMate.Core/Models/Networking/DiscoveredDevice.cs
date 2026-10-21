using System.Net;

namespace ClipMate.Core.Models.Networking;

/// <summary>
/// A Device seen on the local network through its discovery announcements. Seeing a Device grants it no trust.
/// </summary>
/// <param name="DeviceId">The Device ID proven by the announcement's certificate and signature.</param>
/// <param name="DeviceName">The Device Name it announces.</param>
/// <param name="Endpoint">The address the announcement came from, with the Device's Peer listener port.</param>
/// <param name="ProtocolMajor">The Device's Peer protocol major version.</param>
/// <param name="ProtocolMinor">The Device's Peer protocol minor version.</param>
/// <param name="LastSeen">When the latest announcement arrived.</param>
public sealed record DiscoveredDevice(
    DeviceId DeviceId,
    string DeviceName,
    IPEndPoint Endpoint,
    uint ProtocolMajor,
    uint ProtocolMinor,
    DateTimeOffset LastSeen);
