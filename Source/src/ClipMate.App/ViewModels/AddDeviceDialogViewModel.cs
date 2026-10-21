using System.Net;
using System.Net.Sockets;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for <b>Add Device manually</b>: takes the other Device's address (host or IP, plus port) and its Device ID.
/// An address alone is never accepted, because the connection is pinned to the Device ID. Once both are valid,
/// <see cref="Target" /> holds the Device to send the pairing request to.
/// </summary>
public partial class AddDeviceDialogViewModel : ObservableObject
{
    private readonly ILocalDevice _localDevice;
    private readonly IPeerStore _peerStore;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _deviceId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private int _port = NetworkingConfiguration.DefaultPort;

    public AddDeviceDialogViewModel(ILocalDevice localDevice, IPeerStore peerStore)
    {
        _localDevice = localDevice ?? throw new ArgumentNullException(nameof(localDevice));
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
    }

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Gets whether <see cref="ErrorText" /> has something to show.
    /// </summary>
    public bool HasError => ErrorText != null;

    /// <summary>
    /// Gets the Device to pair with once the user clicked <b>Pair</b> with a valid address and Device ID; otherwise <c>null</c>.
    /// Its Device Name is empty until the Device answers.
    /// </summary>
    public PairingTarget? Target { get; private set; }

    [RelayCommand]
    private void Pair()
    {
        Target = null;
        ErrorText = Validate(out var target);
        if (ErrorText != null)
            return;

        Target = target;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Target = null;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private string? Validate(out PairingTarget? target)
    {
        target = null;
        var addressError = TryFormatAddress(Address, Port, out var address);
        if (addressError != null)
            return addressError;

        if (string.IsNullOrWhiteSpace(DeviceId))
            return "Enter the other Device's Device ID as well; an address alone is not enough. It is shown on that Device's Devices page.";

        if (!Core.Models.Networking.DeviceId.TryParse(DeviceId, out var deviceId))
            return "That is not a valid Device ID. A Device ID has 52 letters and digits (A-Z, 2-7), shown in groups of four.";

        var identity = _localDevice.Identity;
        if (identity == null)
            return "Turn on networking and click OK first, then add the Device.";

        if (identity.DeviceId == deviceId.Value)
            return "That is this Device's own Device ID. Enter the Device ID of the Device you want to pair with.";

        if (_peerStore.IsPaired(deviceId.Value))
            return "You are already paired with that Device.";

        target = new PairingTarget(deviceId.Value, string.Empty, address!);
        return null;
    }

    /// <summary>
    /// Builds a <c>host:port</c> address from what the user typed. The host may carry its own port
    /// (<c>host:port</c> or <c>[IPv6]:port</c>), which then wins over <paramref name="port" />.
    /// </summary>
    private static string? TryFormatAddress(string? text, int port, out string? address)
    {
        address = null;
        var host = text?.Trim() ?? string.Empty;
        if (host.Length == 0)
            return "Enter the other Device's host name or IP address.";

        string? portText = null;
        if (host.StartsWith('['))
        {
            // [IPv6] or [IPv6]:port
            var close = host.IndexOf(']');
            var rest = close < 0 ? null : host[(close + 1)..];
            if (rest == null || (rest.Length > 0 && !rest.StartsWith(':')))
                return $"'{host}' is not a valid host name or IP address.";

            portText = rest.Length > 0 ? rest[1..] : null;
            host = host[1..close];
        }
        else if (host.IndexOf(':') is var colon and > 0 && colon == host.LastIndexOf(':'))
        {
            // host:port (a bare IPv6 address has more than one colon)
            portText = host[(colon + 1)..];
            host = host[..colon];
        }

        if (portText != null && !int.TryParse(portText, out port))
            port = 0;

        if (port is < 1 or > IPEndPoint.MaxPort)
            return "Enter a port between 1 and 65535.";

        if (IPAddress.TryParse(host, out var ip))
        {
            address = ip.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{ip}]:{port}" : $"{ip}:{port}";
            return null;
        }

        if (Uri.CheckHostName(host) != UriHostNameType.Dns)
            return $"'{text?.Trim()}' is not a valid host name or IP address.";

        address = $"{host}:{port}";
        return null;
    }
}
