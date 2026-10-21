using System.Diagnostics.CodeAnalysis;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Networking.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClipMate.Networking.DependencyInjection;

/// <summary>
/// Extension methods for registering Peer networking services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Peer host factory, Pairing and LAN discovery. Registration loads none of ASP.NET Core; Kestrel is only
    /// loaded when a host is created. Requires an <see cref="IPeerStore" /> and an <see cref="ILocalDevice" />; the
    /// application should register an <see cref="IPairingApprovalHandler" /> that asks the user (the default declines every request).
    /// </summary>
    public static IServiceCollection AddClipMateNetworking(this IServiceCollection services)
    {
        // Trust comes only from the Peer records.
        services.TryAddSingleton<IPeerCertificateValidator, PeerStoreCertificateValidator>();
        services.TryAddSingleton<IPairingApprovalHandler, DeclineAllPairingApprovalHandler>();
        services.AddSingleton<IPeerHostFactory, KestrelPeerHostFactory>();
        services.AddSingleton<IPairingClient, PeerPairingClient>();
        services.AddSingleton<IUnpairingService, PeerUnpairingService>();
        services.AddSingleton<IPeerProbe, GrpcPeerProbe>();
        services.AddSingleton<IPeerStatusService, PeerStatusMonitor>();

        // LAN discovery opens no socket until networking starts it.
        services.AddSingleton<IDiscoveryTransportFactory, UdpDiscoveryTransportFactory>();
        services.AddSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();
        return services;
    }

    private sealed class DeclineAllPairingApprovalHandler : IPairingApprovalHandler
    {
        public Task<PairingDecision> RequestApprovalAsync(PairingRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(PairingDecision.Declined);
    }
}
