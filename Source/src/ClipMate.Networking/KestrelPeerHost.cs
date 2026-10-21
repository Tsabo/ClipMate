using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

/// <summary>
/// Settings for the Peer listener that are not user configuration.
/// </summary>
public sealed class PeerHostOptions
{
    /// <summary>
    /// Gets the address the listener binds to. Defaults to all IPv4 and IPv6 interfaces.
    /// </summary>
    public IPAddress ListenAddress { get; init; } = IPAddress.IPv6Any;
}

/// <summary>
/// Creates <see cref="KestrelPeerHost" /> instances.
/// </summary>
public sealed class KestrelPeerHostFactory : IPeerHostFactory
{
    private readonly PeerHostServices _services;

    /// <summary>
    /// Creates a factory for hosts that listen on all interfaces.
    /// </summary>
    public KestrelPeerHostFactory(IPeerCertificateValidator validator,
        IPeerStore peerStore,
        IPairingApprovalHandler approvalHandler,
        IConfigurationService configurationService,
        ILoggerFactory loggerFactory)
        : this(validator, peerStore, approvalHandler, configurationService, loggerFactory, new PeerHostOptions())
    {
    }

    /// <summary>
    /// Creates a factory with explicit listener options.
    /// </summary>
    public KestrelPeerHostFactory(IPeerCertificateValidator validator,
        IPeerStore peerStore,
        IPairingApprovalHandler approvalHandler,
        IConfigurationService configurationService,
        ILoggerFactory loggerFactory,
        PeerHostOptions options)
    {
        _services = new PeerHostServices(
            validator ?? throw new ArgumentNullException(nameof(validator)),
            peerStore ?? throw new ArgumentNullException(nameof(peerStore)),
            approvalHandler ?? throw new ArgumentNullException(nameof(approvalHandler)),
            configurationService ?? throw new ArgumentNullException(nameof(configurationService)),
            loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)),
            options ?? throw new ArgumentNullException(nameof(options)),
            new PairingRequestTracker(TimeProvider.System));
    }

    /// <inheritdoc />
    public IPeerHost Create() => new KestrelPeerHost(_services);
}

/// <summary>
/// What a <see cref="KestrelPeerHost" /> needs from the application.
/// </summary>
internal sealed record PeerHostServices(
    IPeerCertificateValidator Validator,
    IPeerStore PeerStore,
    IPairingApprovalHandler ApprovalHandler,
    IConfigurationService ConfigurationService,
    ILoggerFactory LoggerFactory,
    PeerHostOptions Options,
    PairingRequestTracker Tracker);

/// <summary>
/// The Peer listener: a gRPC server on Kestrel that requires TLS client certificates (mutual TLS).
/// <para>
/// A connection between Peers (any TLS server name other than <see cref="PeerTlsServerNames.Pairing" />) completes the
/// TLS handshake only if <see cref="IPeerCertificateValidator" /> trusts the client certificate. A connection made for
/// Pairing accepts any client certificate, so the caller's Device ID is known and the Verification Code can be derived;
/// on it, <see cref="PairedPeerGate" /> serves an unpaired Device nothing but the pairing calls.
/// </para>
/// </summary>
internal sealed class KestrelPeerHost : IPeerHost
{
    private readonly PeerConnectionTracker _connections = new();
    private readonly ILogger<KestrelPeerHost> _logger;
    private readonly PeerHostServices _services;
    private WebApplication? _app;

    public KestrelPeerHost(PeerHostServices services)
    {
        _services = services;
        _logger = services.LoggerFactory.CreateLogger<KestrelPeerHost>();
    }

    /// <inheritdoc />
    public bool IsRunning => _app != null;

    /// <inheritdoc />
    public int? ListeningPort { get; private set; }

    /// <inheritdoc />
    public async Task StartAsync(DeviceIdentity identity, int port, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);
        if (_app != null)
            throw new InvalidOperationException("The Peer host is already running.");

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(KestrelPeerHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_services.LoggerFactory);
        builder.Services.AddSingleton(identity);
        builder.Services.AddSingleton(_services.ConfigurationService);
        builder.Services.AddSingleton(_services.Validator);
        builder.Services.AddSingleton(_services.PeerStore);
        builder.Services.AddSingleton(_services.ApprovalHandler);
        builder.Services.AddSingleton(_services.Tracker);
        builder.Services.AddGrpc(grpc => grpc.Interceptors.Add<PairedPeerGate>());

        builder.WebHost.UseKestrelHttpsConfiguration();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Listen(_services.Options.ListenAddress, port, listen =>
            {
                listen.Protocols = HttpProtocols.Http2;
                listen.UseHttps(new TlsHandshakeCallbackOptions
                {
                    OnConnection = context => ValueTask.FromResult(CreateTlsOptions(identity, context.ClientHelloInfo.ServerName)),
                });
                listen.Use(_connections.Track);
            });
        });

        var app = builder.Build();
        app.MapGrpcService<PeerGrpcService>();

        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        _app = app;
        _services.PeerStore.PeerRemoved += OnPeerRemoved;
        ListeningPort = ReadBoundPort(app) ?? port;
        _logger.LogInformation("Peer host listening on port {Port} as Device {DeviceId}", ListeningPort, identity.DeviceId.ToDisplayString());
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        if (app == null)
            return;

        _services.PeerStore.PeerRemoved -= OnPeerRemoved;
        _app = null;
        ListeningPort = null;
        try
        {
            await app.StopAsync(cancellationToken);
        }
        finally
        {
            await app.DisposeAsync();
            _logger.LogInformation("Peer host stopped");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();

    private void OnPeerRemoved(object? sender, PeerRemovedEventArgs e)
    {
        // Trust is only checked when a connection starts, so close the former Peer's open connections now.
        var dropped = _connections.Drop(e.DeviceId);
        if (dropped > 0)
            _logger.LogInformation("Closed {Count} connection(s) from unpaired Device {DeviceId}", dropped, e.DeviceId.ToDisplayString());
    }

    private SslServerAuthenticationOptions CreateTlsOptions(DeviceIdentity identity, string? serverName)
    {
        var forPairing = string.Equals(serverName, PeerTlsServerNames.Pairing, StringComparison.OrdinalIgnoreCase);
        return new SslServerAuthenticationOptions
        {
            ServerCertificate = identity.Certificate,
            ClientCertificateRequired = true,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck, // self-signed: nothing to check, and no network traffic
            ApplicationProtocols = [SslApplicationProtocol.Http2],
            RemoteCertificateValidationCallback = (_, certificate, _, _) => certificate != null && (forPairing || IsTrusted(certificate)),
        };
    }

    private bool IsTrusted(X509Certificate certificate)
    {
        var deviceId = DeviceId.FromCertificateBytes(certificate.GetRawCertData());
        var trusted = _services.Validator.IsTrusted(deviceId);
        if (!trusted)
            _logger.LogWarning("Rejected connection from Device {DeviceId}, which is not a Peer", deviceId.ToDisplayString());

        return trusted;
    }

    private static int? ReadBoundPort(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
        return address != null && Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Port : null;
    }
}
