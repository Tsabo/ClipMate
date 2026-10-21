using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services;

/// <summary>
/// The listener Peers connect to: an in-process gRPC server secured with mutual TLS (ADR 0002).
/// Created through <see cref="IPeerHostFactory" /> only when networking is enabled, so the networking stack
/// is not loaded while networking is off.
/// </summary>
public interface IPeerHost : IAsyncDisposable
{
    /// <summary>
    /// Gets whether the host is listening.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the TCP port the host is listening on, or <c>null</c> when stopped.
    /// </summary>
    int? ListeningPort { get; }

    /// <summary>
    /// Starts listening on <paramref name="port" /> (0 picks a free port), presenting <paramref name="identity" />'s
    /// certificate and requiring a client certificate that the configured <see cref="IPeerCertificateValidator" /> trusts.
    /// </summary>
    Task StartAsync(DeviceIdentity identity, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops listening and closes open connections.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates <see cref="IPeerHost" /> instances.
/// </summary>
public interface IPeerHostFactory
{
    /// <summary>
    /// Creates a new, stopped host.
    /// </summary>
    IPeerHost Create();
}

/// <summary>
/// Decides whether a connecting Device (identified by the Device ID of its TLS certificate) is allowed to connect.
/// Pairing supplies an implementation backed by the paired Device IDs.
/// </summary>
public interface IPeerCertificateValidator
{
    /// <summary>
    /// Returns <c>true</c> if the Device with <paramref name="deviceId" /> may connect.
    /// </summary>
    bool IsTrusted(DeviceId deviceId);
}
