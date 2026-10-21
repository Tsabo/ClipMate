using System.Collections.ObjectModel;
using System.Net;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Clipboard = System.Windows.Clipboard;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Devices options page: the Enable networking switch, this Device's Device Name and Device ID,
/// and the Discovered Devices on the LAN.
/// </summary>
public partial class DevicesOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly IDeviceDiscoveryService _discovery;
    private readonly IDeviceIdentityService _identityService;
    private readonly ILogger<DevicesOptionsViewModel> _logger;

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
    private int _port;

    [ObservableProperty]
    private bool _showQrCode;

    private SynchronizationContext? _uiContext;

    public DevicesOptionsViewModel(IConfigurationService configurationService,
        IDeviceIdentityService identityService,
        IDeviceDiscoveryService discovery,
        ILogger<DevicesOptionsViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets whether this Device has an identity yet (it is created the first time networking is enabled).
    /// </summary>
    public bool HasDeviceId => DeviceId != null;

    /// <summary>
    /// Gets the Devices currently announcing themselves on the LAN. Seeing a Device here does not pair with it.
    /// </summary>
    public ObservableCollection<DiscoveredDeviceItem> DiscoveredDevices { get; } = [];

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
    /// Shows the Discovered Devices and keeps the list current until <see cref="StopWatchingDiscoveredDevices" />.
    /// Call on the UI thread; updates are posted back to it.
    /// </summary>
    public void StartWatchingDiscoveredDevices()
    {
        _uiContext = SynchronizationContext.Current;
        _discovery.DiscoveredDevicesChanged -= OnDiscoveredDevicesChanged;
        _discovery.DiscoveredDevicesChanged += OnDiscoveredDevicesChanged;
        RefreshDiscoveredDevices();
    }

    /// <summary>
    /// Stops following changes to the Discovered Devices.
    /// </summary>
    public void StopWatchingDiscoveredDevices() => _discovery.DiscoveredDevicesChanged -= OnDiscoveredDevicesChanged;

    private void OnDiscoveredDevicesChanged(object? sender, EventArgs e)
    {
        var context = _uiContext;
        if (context == null)
            RefreshDiscoveredDevices();
        else
            context.Post(_ => RefreshDiscoveredDevices(), null);
    }

    private void RefreshDiscoveredDevices()
    {
        DiscoveredDevices.Clear();
        foreach (var item in _discovery.DiscoveredDevices)
            DiscoveredDevices.Add(new DiscoveredDeviceItem(item.DeviceName, item.DeviceId.ToShortDisplayString(), item.DeviceId.ToDisplayString()));

        HasDiscoveredDevices = DiscoveredDevices.Count > 0;
        DiscoveryStatusText = _discovery.IsRunning
            ? "Looking for your other Devices on this network..."
            : "Turn on networking to find your other Devices on this network.";
    }

    partial void OnEnableNetworkingChanged(bool value)
    {
        if (value)
            _ = EnsureDeviceIdAsync();
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
