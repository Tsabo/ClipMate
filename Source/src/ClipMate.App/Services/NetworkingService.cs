using ClipMate.Core.Events;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services;

/// <summary>
/// Starts and stops the Peer host to follow the <c>Enable networking</c> setting.
/// While networking is off nothing is created, so no port is opened and the networking stack is never loaded.
/// </summary>
public sealed class NetworkingService : IHostedService, IRecipient<PreferencesChangedEvent>, IDisposable
{
    private readonly IConfigurationService _configurationService;
    private readonly IFirewallRuleService _firewallRuleService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IPeerHostFactory _hostFactory;
    private readonly IDeviceIdentityService _identityService;
    private readonly ILogger<NetworkingService> _logger;
    private readonly IMessenger _messenger;
    private bool _disposed;
    private IPeerHost? _host;
    private DeviceIdentity? _identity;
    private int _port;

    public NetworkingService(IConfigurationService configurationService,
        IDeviceIdentityService identityService,
        IFirewallRuleService firewallRuleService,
        IPeerHostFactory hostFactory,
        IMessenger messenger,
        ILogger<NetworkingService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _firewallRuleService = firewallRuleService ?? throw new ArgumentNullException(nameof(firewallRuleService));
        _hostFactory = hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets whether the Peer host is currently listening.
    /// </summary>
    public bool IsListening => _host != null;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _messenger.UnregisterAll(this);
        var host = _host;
        if (host != null)
            Task.Run(async () => await host.DisposeAsync()).GetAwaiter().GetResult(); // off the UI thread's context
        _host = null;
        _identity?.Dispose();
        _identity = null;
        _gate.Dispose();
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messenger.Register(this);

        // Don't hold up application startup (enabling may show the firewall elevation prompt).
        _ = ApplyConfigurationAsync(CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _messenger.Unregister<PreferencesChangedEvent>(this);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopHostAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Receive(PreferencesChangedEvent message) => _ = ApplyConfigurationAsync(CancellationToken.None);

    /// <summary>
    /// Starts, restarts or stops the Peer host so it matches the current networking configuration.
    /// Never throws: failures are logged and reported on the status bar.
    /// </summary>
    public async Task ApplyConfigurationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _gate.WaitAsync(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            var networking = _configurationService.Configuration.Networking;
            if (!networking.Enabled)
            {
                await StopHostAsync();
                return;
            }

            if (_host != null && _port == networking.Port)
                return;

            await StopHostAsync();
            await StartHostAsync(networking.Port, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply networking settings");
            _messenger.Send(new StatusUpdateEvent($"Networking could not be started: {ex.Message}", true));
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StartHostAsync(int port, CancellationToken cancellationToken)
    {
        _identity ??= await _identityService.GetOrCreateAsync(cancellationToken);

        if (!await _firewallRuleService.EnsureInboundRuleAsync(cancellationToken))
            _logger.LogWarning("Windows Firewall rule is missing; Peers may not be able to connect");

        var host = _hostFactory.Create();
        try
        {
            await host.StartAsync(_identity, port, cancellationToken);
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }

        _host = host;
        _port = port;
        _logger.LogInformation("Networking enabled on port {Port}", port);
    }

    private async Task StopHostAsync()
    {
        if (_host == null)
            return;

        var host = _host;
        _host = null;
        await host.DisposeAsync();
        _logger.LogInformation("Networking disabled");
    }
}
