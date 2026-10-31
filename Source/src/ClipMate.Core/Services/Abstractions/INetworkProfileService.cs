namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Reports the network profile (Public, Private or Domain) Windows has given the networks this computer is connected to.
/// ClipMate's firewall rule only lets Peers connect on Private and Domain networks.
/// </summary>
public interface INetworkProfileService
{
    /// <summary>
    /// Gets the names of the connected networks that carry traffic and that Windows treats as Public, where other
    /// Devices cannot connect to ClipMate. Empty when there are none, or when the profiles cannot be read.
    /// </summary>
    IReadOnlyList<string> GetPublicNetworkNames();
}
