using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Reads network profiles from the Windows Network List Manager.
/// </summary>
public sealed class WindowsNetworkProfileService : INetworkProfileService
{
    private const int _enumConnectedNetworks = 1; // NLM_ENUM_NETWORK_CONNECTED
    private const int _categoryPublic = 0; // NLM_NETWORK_CATEGORY_PUBLIC

    // NLM_CONNECTIVITY: subnet, local network or internet traffic over IPv4 or IPv6. A network with none of these (only
    // the NOTRAFFIC flags), such as a Hyper-V virtual switch listed as a Public "Unidentified network", is ignored.
    private const int _carriesTraffic = 0x10 | 0x20 | 0x40 | 0x100 | 0x200 | 0x400;

    private static readonly Guid _networkListManagerClsid = new("DCB00C01-570F-4A9B-8D69-199FDBA5723B");

    private readonly ILogger<WindowsNetworkProfileService> _logger;

    public WindowsNetworkProfileService(ILogger<WindowsNetworkProfileService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetPublicNetworkNames()
    {
        try
        {
            return SelectPublicNetworkNames(ReadConnectedNetworks());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the network profiles");
            return [];
        }
    }

    /// <summary>
    /// The names of the networks in <paramref name="networks" /> that are Public and carry traffic, each once.
    /// </summary>
    internal static IReadOnlyList<string> SelectPublicNetworkNames(IEnumerable<ConnectedNetwork> networks) =>
        networks
            .Where(p => p.Category == _categoryPublic && (p.Connectivity & _carriesTraffic) != 0)
            .Select(p => p.Name)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// Reads every connected network from the Network List Manager.
    /// </summary>
    internal static List<ConnectedNetwork> ReadConnectedNetworks()
    {
        var type = Type.GetTypeFromCLSID(_networkListManagerClsid, true)!;
        dynamic manager = Activator.CreateInstance(type)!;
        var result = new List<ConnectedNetwork>();
        foreach (var item in manager.GetNetworks(_enumConnectedNetworks))
        {
            dynamic network = item;
            result.Add(new ConnectedNetwork((string)network.GetName(), (int)network.GetCategory(), (int)network.GetConnectivity()));
        }

        return result;
    }
}

/// <summary>
/// A connected network as the Network List Manager reports it.
/// </summary>
/// <param name="Name">The network's name, e.g. a Wi-Fi network name.</param>
/// <param name="Category">The NLM_NETWORK_CATEGORY value: 0 Public, 1 Private, 2 Domain.</param>
/// <param name="Connectivity">The NLM_CONNECTIVITY flags.</param>
internal sealed record ConnectedNetwork(string Name, int Category, int Connectivity);
