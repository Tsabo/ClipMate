using ClipMate.Core.Models.Networking;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Creates and loads this Device's identity (key pair and long-lived self-signed certificate).
/// The identity is stored protected for the current Windows user in the ClipMate data folder, never in clipmate.toml.
/// </summary>
public interface IDeviceIdentityService
{
    /// <summary>
    /// Loads the stored identity, or generates and stores a new one on first use.
    /// The caller owns (and disposes) the returned instance.
    /// </summary>
    Task<DeviceIdentity> GetOrCreateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the stored identity without creating one. Returns <c>null</c> if none has been generated yet.
    /// The caller owns (and disposes) the returned instance.
    /// </summary>
    Task<DeviceIdentity?> TryLoadAsync(CancellationToken cancellationToken = default);
}
