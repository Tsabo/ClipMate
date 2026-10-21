using System.Net;
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
    private readonly IConfigurationService _configurationService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PeerHostOptions _options;
    private readonly IPeerCertificateValidator _validator;

    /// <summary>
    /// Creates a factory for hosts that listen on all interfaces.
    /// </summary>
    public KestrelPeerHostFactory(IPeerCertificateValidator validator,
        IConfigurationService configurationService,
        ILoggerFactory loggerFactory)
        : this(validator, configurationService, loggerFactory, new PeerHostOptions())
    {
    }

    /// <summary>
    /// Creates a factory with explicit listener options.
    /// </summary>
    public KestrelPeerHostFactory(IPeerCertificateValidator validator,
        IConfigurationService configurationService,
        ILoggerFactory loggerFactory,
        PeerHostOptions options)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public IPeerHost Create() => new KestrelPeerHost(_validator, _configurationService, _loggerFactory, _options);
}

/// <summary>
/// The Peer listener: a gRPC server on Kestrel that requires TLS client certificates (mutual TLS)
/// and accepts only Devices that <see cref="IPeerCertificateValidator" /> trusts.
/// </summary>
internal sealed class KestrelPeerHost : IPeerHost
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<KestrelPeerHost> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PeerHostOptions _options;
    private readonly IPeerCertificateValidator _validator;
    private WebApplication? _app;

    public KestrelPeerHost(IPeerCertificateValidator validator,
        IConfigurationService configurationService,
        ILoggerFactory loggerFactory,
        PeerHostOptions options)
    {
        _validator = validator;
        _configurationService = configurationService;
        _loggerFactory = loggerFactory;
        _options = options;
        _logger = loggerFactory.CreateLogger<KestrelPeerHost>();
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
        builder.Services.AddSingleton(_loggerFactory);
        builder.Services.AddSingleton(identity);
        builder.Services.AddSingleton(_configurationService);
        builder.Services.AddGrpc();

        builder.WebHost.UseKestrelHttpsConfiguration();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Listen(_options.ListenAddress, port, listen =>
            {
                listen.Protocols = HttpProtocols.Http2;
                listen.UseHttps(https =>
                {
                    https.ServerCertificate = identity.Certificate;
                    https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    https.CheckCertificateRevocation = false; // self-signed: nothing to check, and no network traffic
                    https.ClientCertificateValidation = (certificate, _, _) => IsTrusted(certificate);
                });
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
        ListeningPort = ReadBoundPort(app) ?? port;
        _logger.LogInformation("Peer host listening on port {Port} as Device {DeviceId}", ListeningPort, identity.DeviceId.ToDisplayString());
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        if (app == null)
            return;

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

    private bool IsTrusted(X509Certificate2 certificate)
    {
        var deviceId = DeviceId.FromCertificate(certificate);
        var trusted = _validator.IsTrusted(deviceId);
        if (!trusted)
            _logger.LogWarning("Rejected connection from untrusted Device {DeviceId}", deviceId.ToDisplayString());

        return trusted;
    }

    private static int? ReadBoundPort(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
        return address != null && Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Port : null;
    }
}
