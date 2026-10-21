using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using ClipMate.App.Services;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Clipboard = System.Windows.Clipboard;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Devices options page: the Enable networking switch, this Device's Device Name and Device ID,
/// the Discovered Devices on the LAN (with <b>Pair</b>) and the Peers with their status (with <b>Rename…</b> and <b>Unpair</b>).
/// </summary>
public partial class DevicesOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly IDeviceDiscoveryService _discovery;
    private readonly IDeviceIdentityService _identityService;
    private readonly ILogger<DevicesOptionsViewModel> _logger;
    private readonly INetworkProfileService _networkProfiles;
    private readonly IPairingDialogService _pairingDialogs;
    private readonly IPeerDialogService _peerDialogs;
    private readonly IPeerStatusService _peerStatus;
    private readonly IPeerStore _peerStore;
    private readonly IUnpairingService _unpairing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDeviceId))]
    [NotifyCanExecuteChangedFor(nameof(CopyDeviceIdCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleQrCodeCommand))]
    private string? _deviceId;

    [ObservableProperty]
    private string? _deviceIdQrText;

    private Task? _deviceIdTask;

    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private string _discoveryStatusText = string.Empty;

    [ObservableProperty]
    private bool _enableNetworking;

    [ObservableProperty]
    private bool _hasDiscoveredDevices;

    [ObservableProperty]
    private bool _hasPeers;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPublicNetworkWarning))]
    private string? _publicNetworkWarning;

    private static readonly TimeSpan _networkWarningInterval = TimeSpan.FromSeconds(5);
    private System.Threading.Timer? _networkWarningTimer;

    [ObservableProperty]
    private bool _showQrCode;

    private SynchronizationContext? _uiContext;

    public DevicesOptionsViewModel(IConfigurationService configurationService,
        IDeviceIdentityService identityService,
        IDeviceDiscoveryService discovery,
        IPeerStore peerStore,
        IPeerStatusService peerStatus,
        IPairingDialogService pairingDialogs,
        IPeerDialogService peerDialogs,
        IUnpairingService unpairing,
        INetworkProfileService networkProfiles,
        ILogger<DevicesOptionsViewModel> logger)
    {
        _networkProfiles = networkProfiles ?? throw new ArgumentNullException(nameof(networkProfiles));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _peerStore = peerStore ?? throw new ArgumentNullException(nameof(peerStore));
        _peerStatus = peerStatus ?? throw new ArgumentNullException(nameof(peerStatus));
        _pairingDialogs = pairingDialogs ?? throw new ArgumentNullException(nameof(pairingDialogs));
        _peerDialogs = peerDialogs ?? throw new ArgumentNullException(nameof(peerDialogs));
        _unpairing = unpairing ?? throw new ArgumentNullException(nameof(unpairing));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets whether this Device has an identity yet (it is created the first time networking is enabled).
    /// </summary>
    public bool HasDeviceId => DeviceId != null;

    /// <summary>
    /// Gets whether <see cref="PublicNetworkWarning" /> should be shown.
    /// </summary>
    public bool HasPublicNetworkWarning => PublicNetworkWarning != null;

    /// <summary>
    /// Gets the Devices currently announcing themselves on the LAN. Seeing a Device here does not pair with it.
    /// </summary>
    public ObservableCollection<DiscoveredDeviceItem> DiscoveredDevices { get; } = [];

    /// <summary>
    /// Gets the Peers with their Peer Alias, Device Name, short Device ID and status.
    /// </summary>
    public ObservableCollection<PeerItem> Peers { get; } = [];

    /// <summary>
    /// Loads the networking settings and, if one exists, the Device ID. Never creates an identity.
    /// </summary>
    public async Task LoadAsync()
    {
        var networking = _configurationService.Configuration.Networking;
        DeviceName = networking.DeviceName;
        Port = networking.Port;
        ShowQrCode = false;

        // Set the backing field so loading does not trigger identity generation.
#pragma warning disable MVVMTK0034
        _enableNetworking = networking.Enabled;
#pragma warning restore MVVMTK0034
        OnPropertyChanged(nameof(EnableNetworking));
        RefreshNetworkWarning();

        _deviceIdTask = null;
        await LoadDeviceIdAsync(false);
        if (EnableNetworking && !HasDeviceId)
            await EnsureDeviceIdAsync();
    }

    /// <summary>
    /// Saves the networking settings. The Peer host follows them once the Options dialog broadcasts its changes.
    /// </summary>
    public void Save()
    {
        var networking = _configurationService.Configuration.Networking;
        networking.Enabled = EnableNetworking;
        networking.DeviceName = string.IsNullOrWhiteSpace(DeviceName) ? Environment.MachineName : DeviceName.Trim();
        networking.Port = Port is >= 1 and <= IPEndPoint.MaxPort ? Port : Core.Models.Configuration.NetworkingConfiguration.DefaultPort;
    }

    /// <summary>
    /// Makes sure the Device identity exists and its Device ID is shown, generating the identity on first use.
    /// </summary>
    public Task EnsureDeviceIdAsync() => HasDeviceId ? Task.CompletedTask : _deviceIdTask ??= LoadDeviceIdAsync(true);

    /// <summary>
    /// Shows the Discovered Devices and the Peers and keeps both lists current until <see cref="StopWatching" />.
    /// Call on the UI thread; updates are posted back to it.
    /// </summary>
    public void StartWatching()
    {
        _uiContext = SynchronizationContext.Current;
        StopWatching();
        _discovery.DiscoveredDevicesChanged += OnListsChanged;
        _peerStore.PeersChanged += OnListsChanged;
        _peerStatus.StatusChanged += OnListsChanged;
        RefreshLists();

        // Windows raises no event ClipMate can use when a network's profile changes, so check while the page is open
        _networkWarningTimer = new System.Threading.Timer(_ => OnNetworkCheckDue(), null, _networkWarningInterval, _networkWarningInterval);
    }

    /// <summary>
    /// Stops following changes to the Discovered Devices and Peers.
    /// </summary>
    public void StopWatching()
    {
        _discovery.DiscoveredDevicesChanged -= OnListsChanged;
        _peerStore.PeersChanged -= OnListsChanged;
        _peerStatus.StatusChanged -= OnListsChanged;
        _networkWarningTimer?.Dispose();
        _networkWarningTimer = null;
    }

    private void OnNetworkCheckDue()
    {
        var context = _uiContext;
        if (context == null)
            RefreshNetworkWarning();
        else
            context.Post(_ => RefreshNetworkWarning(), null);
    }

    private void OnListsChanged(object? sender, EventArgs e)
    {
        var context = _uiContext;
        if (context == null)
            RefreshLists();
        else
            context.Post(_ => RefreshLists(), null);
    }

    private void RefreshLists()
    {
        RefreshPeers();
        RefreshDiscoveredDevices();
    }

    private void RefreshPeers()
    {
        var now = DateTime.Now;
        Peers.Clear();
        foreach (var item in _peerStore.GetPeers().OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            Peers.Add(PeerItem.From(item, _peerStatus.GetStatus(item.DeviceId), now));

        HasPeers = Peers.Count > 0;
    }

    private void RefreshDiscoveredDevices()
    {
        DiscoveredDevices.Clear();
        foreach (var item in _discovery.DiscoveredDevices)
            DiscoveredDevices.Add(ToItem(item));

        HasDiscoveredDevices = DiscoveredDevices.Count > 0;
        DiscoveryStatusText = _discovery.IsRunning
            ? "Looking for your other Devices on this network..."
            : "Turn on networking to find your other Devices on this network.";
    }

    private DiscoveredDeviceItem ToItem(DiscoveredDevice device)
    {
        var paired = _peerStore.IsPaired(device.DeviceId);
        var compatible = device.ProtocolMajor == Networking.PeerProtocol.Major;
        var status = paired ? "Paired" : compatible ? string.Empty : "Update required";
        return new DiscoveredDeviceItem(device.DeviceName, device.DeviceId.ToShortDisplayString(), device.DeviceId.ToDisplayString(), status,
            !paired && compatible && _discovery.IsRunning, device);
    }

    [RelayCommand]
    private async Task PairAsync(DiscoveredDeviceItem? item)
    {
        if (item is not { CanPair: true })
            return;

        try
        {
            var device = item.Device;
            await _pairingDialogs.PairAsync(new PairingTarget(device.DeviceId, device.DeviceName, device.Endpoint.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pairing with {DeviceName} failed", item.DeviceName);
        }

        RefreshLists();
    }

    [RelayCommand]
    private async Task RenamePeerAsync(PeerItem? item)
    {
        if (item == null)
            return;

        var alias = _peerDialogs.AskPeerAlias(item.Peer);
        if (alias == null)
            return;

        try
        {
            await _peerStore.SetAliasAsync(item.Peer.DeviceId, alias);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Renaming Peer {DeviceName} failed", item.DeviceName);
        }
    }

    [RelayCommand]
    private async Task UnpairAsync(PeerItem? item)
    {
        if (item == null || !_peerDialogs.ConfirmUnpair(item.Peer))
            return;

        try
        {
            await _unpairing.UnpairAsync(item.Peer.DeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unpairing {DeviceName} failed", item.DeviceName);
        }

        RefreshLists();
    }

    [RelayCommand]
    private async Task AddDeviceManuallyAsync()
    {
        try
        {
            await _pairingDialogs.AddDeviceManuallyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Adding a Device manually failed");
        }

        RefreshLists();
    }

    partial void OnEnableNetworkingChanged(bool value)
    {
        if (value)
            _ = EnsureDeviceIdAsync();

        RefreshNetworkWarning();
    }

    /// <summary>
    /// Shows or clears the warning that Peers cannot connect because this computer is on a Public network.
    /// Only relevant while networking is on.
    /// </summary>
    public void RefreshNetworkWarning()
    {
        var names = EnableNetworking ? _networkProfiles.GetPublicNetworkNames() : [];
        PublicNetworkWarning = names.Count == 0 ? null : BuildPublicNetworkWarning(names);
    }

    private static string BuildPublicNetworkWarning(IReadOnlyList<string> names)
    {
        var quoted = string.Join(" and ", names.Select(p => $"\"{p}\""));
        var network = names.Count == 1 ? "network" : "networks";
        return $"Your other Devices can't connect to this Device: Windows treats {quoted} as a Public {network}, and ClipMate only accepts " +
               "Peer connections on Private and Domain networks. This Device can still connect to its Peers. " +
               "If you trust this network, set it to Private in Windows Settings: Network & internet > your Wi-Fi or Ethernet " +
               "connection > Network profile type > Private.";
    }

    [RelayCommand]
    private void OpenNetworkSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:network-status") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the Windows network settings");
        }
    }

    private async Task LoadDeviceIdAsync(bool create)
    {
        try
        {
            using var identity = create
                ? await _identityService.GetOrCreateAsync()
                : await _identityService.TryLoadAsync();

            DeviceId = identity?.DeviceId.ToDisplayString();
            DeviceIdQrText = identity?.DeviceId.Value;
        }
        catch (Exception ex)
        {
            _deviceIdTask = null;
            _logger.LogError(ex, "Failed to load the Device identity");
        }
    }

    [RelayCommand(CanExecute = nameof(HasDeviceId))]
    private void CopyDeviceId()
    {
        try
        {
            Clipboard.SetText(DeviceId!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy the Device ID to the clipboard");
        }
    }

    [RelayCommand(CanExecute = nameof(HasDeviceId))]
    private void ToggleQrCode() => ShowQrCode = !ShowQrCode;
}
