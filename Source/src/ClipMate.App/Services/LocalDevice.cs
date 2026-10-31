using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.App.Services;

/// <summary>
/// This Device's networking state, updated by <see cref="NetworkingService" /> as the Peer listener starts and stops.
/// Kept apart from <see cref="NetworkingService" /> because the services that only read it (Remote Databases, Pairing)
/// are themselves reached from the Peer host that <see cref="NetworkingService" /> owns; depending on the lifecycle
/// service would make a dependency cycle that deadlocks startup.
/// </summary>
public sealed class LocalDevice : ILocalDevice
{
    private readonly IConfigurationService _configurationService;
    private volatile State _state = State.Stopped;

    public LocalDevice(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    /// <inheritdoc />
    public DeviceIdentity? Identity => _state.Identity;

    /// <inheritdoc />
    public string DeviceName => _configurationService.Configuration.Networking.DeviceName;

    /// <inheritdoc />
    public int? ListeningPort => _state.ListeningPort;

    /// <summary>
    /// Records that the Peer listener is listening as <paramref name="identity" /> on <paramref name="listeningPort" />.
    /// </summary>
    internal void SetListening(DeviceIdentity identity, int? listeningPort) =>
        _state = new State(identity ?? throw new ArgumentNullException(nameof(identity)), listeningPort);

    /// <summary>
    /// Records that the Peer listener has stopped.
    /// </summary>
    internal void SetStopped() => _state = State.Stopped;

    // Identity and port change together, so readers never see one without the other
    private sealed record State(DeviceIdentity? Identity, int? ListeningPort)
    {
        public static readonly State Stopped = new(null, null);
    }
}
