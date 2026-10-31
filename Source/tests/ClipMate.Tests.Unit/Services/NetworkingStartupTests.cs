using ClipMate.App.Services;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Networking.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// With networking off, starting ClipMate must not load the networking stack (ASP.NET Core / Kestrel).
/// No other unit test creates a Peer host, so ASP.NET Core assemblies are never loaded in this process otherwise.
/// </summary>
[NotInParallel]
public class NetworkingStartupTests
{
    [Test]
    public async Task StartAsync_NetworkingOff_DoesNotLoadAspNetCore()
    {
        // Arrange
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(configurationService);
        services.AddSingleton(A.Fake<IDeviceIdentityService>());
        services.AddSingleton(A.Fake<IFirewallRuleService>());
        services.AddSingleton(A.Fake<IPeerStore>());
        services.AddSingleton(A.Fake<ISharedDatabaseService>());
        services.AddSingleton(A.Fake<ISharedDatabaseChangeFeed>());
        services.AddSingleton<IMessenger>(new StrongReferenceMessenger());
        services.AddSingleton<NetworkingService>();
        services.AddSingleton<LocalDevice>();
        services.AddSingleton<ILocalDevice>(p => p.GetRequiredService<LocalDevice>());
        services.AddClipMateNetworking();
        await using var provider = services.BuildServiceProvider();

        // Act
        var networking = provider.GetRequiredService<NetworkingService>();
        await networking.StartAsync(CancellationToken.None);
        await networking.ApplyConfigurationAsync();

        // The collection tree asks for Remote Databases at startup
        var remoteDatabases = provider.GetRequiredService<IRemoteDatabaseService>();
        await remoteDatabases.RefreshAsync();
        await Assert.That(remoteDatabases.GetRemoteDatabases()).IsEmpty();

        // Assert
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Select(p => p.GetName().Name)
            .Where(p => p != null && (p.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || p.StartsWith("Grpc", StringComparison.Ordinal)))
            .ToList();
        await Assert.That(loaded).IsEmpty();
        await Assert.That(networking.IsListening).IsFalse();
    }
}
