using ClipMate.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipMate.Networking.Internal;

/// <summary>
/// What a <see cref="KestrelPeerHost" /> needs from the application.
/// </summary>
internal sealed record PeerHostServices(
    IPeerCertificateValidator Validator,
    IPeerStore PeerStore,
    PeerPairing Pairing,
    IConfigurationService ConfigurationService,
    ISharedDatabaseService SharedDatabases,
    ISharedDatabaseChangeFeed ChangeFeed,
    ILoggerFactory LoggerFactory,
    PeerHostOptions Options);
