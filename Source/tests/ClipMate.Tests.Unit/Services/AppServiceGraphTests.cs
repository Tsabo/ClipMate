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
public partial class AppServiceGraphTests
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

    [Test]
    public async Task EveryRequiredService_TheSourcesAskFor_IsRegistered()
    {
        // Arrange: each GetRequiredService<T>() and GetRequiredService(typeof(T)) in Source/src, with T found by name
        await using var provider = BuildApplicationServices();
        var isService = provider.GetRequiredService<IServiceProviderIsService>();
        var types = KnownTypes();
        var sourceDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", "..", "..", "src"));
        var separator = Path.DirectorySeparatorChar;

        var requests = Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{separator}obj{separator}") && !p.Contains($"{separator}bin{separator}"))
            .SelectMany(p => RequiredServiceCall().Matches(File.ReadAllText(p)).Select(m => (File: Path.GetRelativePath(sourceDirectory, p), Name: m.Groups["type"].Value)))
            .Where(p => !_requestedFromAnotherContainer.Contains(p.Name))
            .Distinct()
            .ToList();

        // Act
        var unregistered = requests
            .Where(p => Resolve(p.Name, types) is not { } type || !isService.IsService(type))
            .Select(p => $"{p.Name} ({p.File})")
            .ToList();

        // Assert
        await Assert.That(requests).IsNotEmpty();
        await Assert.That(unregistered).IsEmpty();
    }

    // Asked for from a container other than the application's, which the test doesn't build
    private static readonly HashSet<string> _requestedFromAnotherContainer =
    [
        "IServer", // the Peer host's Kestrel container (ClipMate.Networking)
    ];

    // "Name" or "Name<Argument>"; the argument is one type name
    private static Type? Resolve(string name, ILookup<string, Func<Type?>> types)
    {
        var open = name.IndexOf('<');
        if (open < 0)
            return types[name].Select(p => p()).FirstOrDefault(p => p != null);

        var definition = types[$"{name[..open]}`1"].Select(p => p()).FirstOrDefault(p => p != null);
        var argument = types[name[(open + 1)..^1]].Select(p => p()).FirstOrDefault(p => p != null);
        return definition != null && argument != null ? definition.MakeGenericType(argument) : null;
    }

    // Every type by simple name, loaded only when asked for. ClipMate's own types are read from metadata: reflecting
    // over them all would load ASP.NET Core (Networking derives from it), which StartAsync_NetworkingOff_DoesNotLoadAspNetCore
    // forbids. Framework types come from the assemblies building the container already loaded.
    private static ILookup<string, Func<Type?>> KnownTypes()
    {
        var clipMate = new[] { typeof(global::ClipMate.App.App).Assembly, typeof(IClipService).Assembly, typeof(global::ClipMate.Data.ClipMateDbContext).Assembly,
            typeof(global::ClipMate.Platform.Services.ClipboardService).Assembly, typeof(NetworkingService).Assembly };

        var ownTypes = clipMate.SelectMany(assembly =>
        {
            using var stream = File.OpenRead(assembly.Location);
            using var reader = new System.Reflection.PortableExecutable.PEReader(stream);
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(reader);
            return metadata.TypeDefinitions
                .Select(metadata.GetTypeDefinition)
                .Where(p => p.GetDeclaringType().IsNil) // top-level types; services aren't nested
                .Select(p => (Name: metadata.GetString(p.Name), FullName: $"{metadata.GetString(p.Namespace)}.{metadata.GetString(p.Name)}"))
                .Select(p => (p.Name, Load: (Func<Type?>)(() => assembly.GetType(p.FullName))))
                .ToList();
        });

        var frameworkTypes = new[] { typeof(ILogger<>).Assembly, typeof(CommunityToolkit.Mvvm.Messaging.IMessenger).Assembly }
            .Concat(AppDomain.CurrentDomain.GetAssemblies())
            .Distinct()
            .Where(p => !p.IsDynamic && p.GetName().Name?.StartsWith("ClipMate", StringComparison.Ordinal) != true)
            .SelectMany(p =>
            {
                try
                {
                    return p.GetExportedTypes();
                }
                catch (Exception ex) when (ex is NotSupportedException or System.Reflection.ReflectionTypeLoadException or FileNotFoundException)
                {
                    return [];
                }
            })
            .Select(p => (p.Name, Load: (Func<Type?>)(() => p)));

        return ownTypes.Concat(frameworkTypes).ToLookup(p => p.Name, p => p.Load);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"GetRequiredService(?:<(?<type>[\w.]+(?:<[\w.]+>)?)>\(|\(typeof\((?<type>[\w.]+)\)\))")]
    private static partial System.Text.RegularExpressions.Regex RequiredServiceCall();

    private static string SourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

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
