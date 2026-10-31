using System.Net;

namespace ClipMate.Networking.Discovery.Abstraction;

/// <summary>
/// A received discovery datagram and where it came from.
/// </summary>
public readonly record struct DiscoveryDatagram(ReadOnlyMemory<byte> Data, IPEndPoint Source);
