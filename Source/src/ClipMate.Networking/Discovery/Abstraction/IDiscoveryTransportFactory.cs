namespace ClipMate.Networking.Discovery.Abstraction;

/// <summary>
/// Opens <see cref="IDiscoveryTransport" />s.
/// </summary>
public interface IDiscoveryTransportFactory
{
    /// <summary>
    /// Opens a transport that sends to and listens on UDP <paramref name="port" />.
    /// </summary>
    IDiscoveryTransport Create(int port);
}
