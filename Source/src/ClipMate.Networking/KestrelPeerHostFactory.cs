using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.Internal;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking;

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
        ISharedDatabaseService sharedDatabases,
        ISharedDatabaseChangeFeed changeFeed,
        ILoggerFactory loggerFactory)
        : this(validator, peerStore, approvalHandler, configurationService, sharedDatabases, changeFeed, loggerFactory, new PeerHostOptions())
    {
    }

    /// <summary>
    /// Creates a factory with explicit listener options.
    /// </summary>
    public KestrelPeerHostFactory(IPeerCertificateValidator validator,
        IPeerStore peerStore,
        IPairingApprovalHandler approvalHandler,
        IConfigurationService configurationService,
        ISharedDatabaseService sharedDatabases,
        ISharedDatabaseChangeFeed changeFeed,
        ILoggerFactory loggerFactory,
        PeerHostOptions options)
    {
        _services = new PeerHostServices(
            validator ?? throw new ArgumentNullException(nameof(validator)),
            peerStore ?? throw new ArgumentNullException(nameof(peerStore)),
            approvalHandler ?? throw new ArgumentNullException(nameof(approvalHandler)),
            configurationService ?? throw new ArgumentNullException(nameof(configurationService)),
            sharedDatabases ?? throw new ArgumentNullException(nameof(sharedDatabases)),
            changeFeed ?? throw new ArgumentNullException(nameof(changeFeed)),
            loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)),
            options ?? throw new ArgumentNullException(nameof(options)),
            new PairingRequestTracker(TimeProvider.System));
    }

    /// <inheritdoc />
    public IPeerHost Create() => new KestrelPeerHost(_services);
}
