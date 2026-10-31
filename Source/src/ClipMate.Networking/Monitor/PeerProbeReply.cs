namespace ClipMate.Networking.Monitor;

/// <summary>
/// What a Peer reported about itself in Hello.
/// </summary>
/// <param name="DeviceName">The Peer's current Device Name.</param>
/// <param name="ProtocolMajor">The Peer's protocol major version.</param>
/// <param name="ProtocolMinor">The Peer's protocol minor version.</param>
/// <param name="Capabilities">The optional features the Peer supports.</param>
public sealed record PeerProbeReply(string DeviceName, uint ProtocolMajor, uint ProtocolMinor, IReadOnlyList<string> Capabilities);
