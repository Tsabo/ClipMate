using ClipMate.App.DependencyInjection;
using ClipMate.App.Services;
using ClipMate.Core.DependencyInjection;
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

    /// <summary>
    /// The registrations App.CreateHostBuilder makes, with configuration that touches no real user files.
    /// </summary>
    private static ServiceProvider BuildApplicationServices()
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
        return services.BuildServiceProvider();
    }
}
