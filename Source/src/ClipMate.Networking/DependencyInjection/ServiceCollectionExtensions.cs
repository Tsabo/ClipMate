using System.Diagnostics.CodeAnalysis;
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
    /// Registers the Peer host factory and LAN discovery. Registration loads none of ASP.NET Core; Kestrel is only loaded when a host is created.
    /// </summary>
    public static IServiceCollection AddClipMateNetworking(this IServiceCollection services)
    {
        services.TryAddSingleton<IPeerCertificateValidator, RejectAllPeerCertificateValidator>();
        services.AddSingleton<IPeerHostFactory, KestrelPeerHostFactory>();

        // LAN discovery opens no socket until networking starts it.
        services.AddSingleton<IDiscoveryTransportFactory, UdpDiscoveryTransportFactory>();
        services.AddSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();
        return services;
    }
}
