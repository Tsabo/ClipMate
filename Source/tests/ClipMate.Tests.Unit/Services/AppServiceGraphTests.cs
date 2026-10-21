using ClipMate.App.DependencyInjection;
using ClipMate.App.Services;
using ClipMate.Core.DependencyInjection;
using ClipMate.Core.Helpers;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.DependencyInjection;
using ClipMate.Networking.DependencyInjection;
using ClipMate.Platform.DependencyInjection;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Resolves services from the application's real service registrations. Factory registrations hide dependency cycles
/// from the container's own cycle check, and such a cycle deadlocks startup instead of failing it.
/// </summary>
[NotInParallel]
public class AppServiceGraphTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);
    private string? _savedDataDirectory;

    // Services that touch the data folder (configuration, Device identity) never see the user's own
    [Before(Test)]
    public void UseIsolatedDataDirectory()
    {
        _savedDataDirectory = Environment.GetEnvironmentVariable(ClipMatePaths.DataDirectoryVariable);
        Environment.SetEnvironmentVariable(ClipMatePaths.DataDirectoryVariable,
            Path.Join(Path.GetTempPath(), $"clipmate-graph-{Guid.NewGuid():N}"));
    }

    [After(Test)]
    public void RestoreDataDirectory() => Environment.SetEnvironmentVariable(ClipMatePaths.DataDirectoryVariable, _savedDataDirectory);

    [Test]
    [Arguments(typeof(IRemoteDatabaseService))]
    [Arguments(typeof(NetworkingService))]
    [Arguments(typeof(ILocalDevice))]
    [Arguments(typeof(IClipService))]
    [Arguments(typeof(IClipContentService))]
    [Arguments(typeof(ISharedDatabaseService))]
    [Arguments(typeof(IPeerHostFactory))]
    [Arguments(typeof(IPairingClient))]
    public async Task Resolve_FromTheApplicationRegistrations_CompletesWithoutACycle(Type serviceType)
    {
        // Arrange
        await using var provider = BuildApplicationServices();

        // Act: off the test thread, so a deadlock shows up as a timeout instead of hanging the run
        var resolve = Task.Run(() => provider.GetRequiredService(serviceType));
        var completed = await Task.WhenAny(resolve, Task.Delay(_timeout));

        // Assert
        await Assert.That(completed).IsSameReferenceAs(resolve);
        await Assert.That(await resolve).IsNotNull();
    }

    [Test]
    public async Task ResolveEveryService_FromTheApplicationRegistrations_CompletesWithoutACycle()
    {
        // Arrange: every closed service type except WPF objects (windows, controls), which need the UI thread
        var services = CreateApplicationServices();
        var serviceTypes = services
            .Where(p => !p.ServiceType.ContainsGenericParameters && !IsWpfObject(p))
            .Select(p => p.ServiceType)
            .Distinct()
            .ToList();

        await using var provider = services.BuildServiceProvider();
        var failures = new List<string>();

        // Act: off the test thread, so a deadlock shows up as a timeout instead of hanging the run
        var resolveAll = Task.Run(() =>
        {
            foreach (var item in serviceTypes)
            {
                try
                {
                    _ = provider.GetServices(item).ToList();
                }
                catch (Exception ex)
                {
                    failures.Add($"{item.FullName}: {ex.GetBaseException().Message}");
                }
            }
        });

        var completed = await Task.WhenAny(resolveAll, Task.Delay(_timeout * 2));

        // Assert
        await Assert.That(completed).IsSameReferenceAs(resolveAll);
        await Assert.That(failures).IsEmpty();
    }

    // Windows and controls need the UI thread; IWindow is registered through a factory, so its type isn't visible
    private static bool IsWpfObject(ServiceDescriptor descriptor)
    {
        var type = descriptor.ImplementationType ?? descriptor.ServiceType;
        return typeof(System.Windows.DependencyObject).IsAssignableFrom(type) || typeof(IWindow).IsAssignableFrom(descriptor.ServiceType);
    }

    /// <summary>
    /// The registrations App.CreateHostBuilder makes, with configuration that touches no real user files.
    /// </summary>
    private static ServiceProvider BuildApplicationServices() => CreateApplicationServices().BuildServiceProvider();

    private static ServiceCollection CreateApplicationServices()
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(new EventLogSink());
        services.AddClipMateCore();
        services.AddClipMateData(Path.Join(Path.GetTempPath(), $"clipmate-graph-{Guid.NewGuid():N}.db"));
        services.AddClipMatePlatform();
        services.AddClipMateNetworking();
        services.AddClipMateApp();
        services.AddSingleton(configurationService);
        return services;
    }
}
