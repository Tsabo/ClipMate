using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ClipMate.Core.Models.Networking;

/// <summary>
/// The routing key for a Remote Database inside services and view models that take a <c>databaseKey</c> string.
/// <para>
/// It has the form <c>remote:&lt;Device ID&gt;:&lt;Database ID&gt;</c>. It never contains a path separator and never
/// starts with <c>db_</c>, so it cannot be mistaken for a database file or a database configuration entry. Local data
/// access refuses it (Remote Databases have no local file and are read-only); reads are routed to the Peer instead.
/// </para>
/// </summary>
public static class RemoteDatabaseKey
{
    private const string _prefix = "remote:";

    /// <summary>
    /// Gets the routing key of the Remote Database at <paramref name="address" />.
    /// </summary>
    public static string From(RemoteDatabaseAddress address) =>
        string.Create(CultureInfo.InvariantCulture, $"{_prefix}{address.DeviceId.Value}:{address.DatabaseId:N}");

    /// <summary>
    /// Reads the Remote Database address from a routing key.
    /// </summary>
    /// <returns><c>true</c> if <paramref name="databaseKey" /> is a Remote Database routing key.</returns>
    public static bool TryParse([NotNullWhen(true)] string? databaseKey, out RemoteDatabaseAddress address)
    {
        address = default;
        if (databaseKey == null || !databaseKey.StartsWith(_prefix, StringComparison.Ordinal))
            return false;

        var parts = databaseKey[_prefix.Length..].Split(':');
        if (parts.Length != 2 || !DeviceId.TryParse(parts[0], out var deviceId) ||
            !Guid.TryParseExact(parts[1], "N", out var databaseId))
            return false;

        address = new RemoteDatabaseAddress(deviceId.Value, databaseId);
        return true;
    }

    /// <summary>
    /// Gets whether <paramref name="databaseKey" /> addresses a Remote Database (and is therefore read-only).
    /// </summary>
    public static bool IsRemote([NotNullWhen(true)] string? databaseKey) => TryParse(databaseKey, out _);
}
