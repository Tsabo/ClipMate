using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// <see cref="IPeerStore" /> backed by the <c>peers</c> section of clipmate.toml.
/// Changes replace the list and records instead of mutating them, so a concurrent save never sees a half-changed list.
/// </summary>
public sealed class PeerStore : IPeerStore
{
    private const int _maxAddresses = 4;
    private readonly IConfigurationService _configurationService;
    private readonly Lock _lock = new();
    private readonly ILogger<PeerStore> _logger;

    public PeerStore(IConfigurationService configurationService, ILogger<PeerStore> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler? PeersChanged;

    /// <inheritdoc />
    public event EventHandler<PeerRemovedEventArgs>? PeerRemoved;

    /// <inheritdoc />
    public IReadOnlyList<Peer> GetPeers()
    {
        var records = Records;
        var peers = new List<Peer>(records.Count);
        foreach (var item in records)
        {
            var peer = ToPeer(item);
            if (peer != null)
                peers.Add(peer);
        }

        return peers;
    }

    /// <inheritdoc />
    public Peer? Find(DeviceId deviceId)
    {
        var record = FindRecord(Records, deviceId);
        return record == null ? null : ToPeer(record);
    }

    /// <inheritdoc />
    public bool IsPaired(DeviceId deviceId) => FindRecord(Records, deviceId) != null;

    /// <inheritdoc />
    public Task SavePairedAsync(DeviceId deviceId, string deviceName, string? alias, string? address,
        CancellationToken cancellationToken = default) =>
        SavePairedAsync(deviceId, deviceName, alias, address, false, cancellationToken);

    /// <inheritdoc />
    public async Task SavePairedAsync(DeviceId deviceId, string deviceName, string? alias, string? address, bool pinAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceName);
        lock (_lock)
        {
            var existing = FindRecord(Records, deviceId);
            var record = new PeerConfiguration
            {
                DeviceId = deviceId.Value,
                DeviceName = deviceName,
                Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim(),
                Addresses = WithAddress(existing?.Addresses ?? [], address),
                ManualAddresses = pinAddress && !string.IsNullOrWhiteSpace(address)
                    ? WithManualAddress(existing?.ManualAddresses ?? [], address)
                    : existing?.ManualAddresses ?? [],
                LastSeen = existing?.LastSeen,
                PairedAt = DateTime.Now,
                SharedDatabases = existing?.SharedDatabases ?? [],
                DatabaseAliases = existing?.DatabaseAliases ?? [],
            };
            Replace(deviceId, record);
        }

        _logger.LogInformation("Paired with Device {DeviceId}", deviceId.ToDisplayString());
        await _configurationService.SaveAsync(cancellationToken);
        PeersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task RecordSeenAsync(DeviceId deviceId, string deviceName, string? address, DateTime seenAt,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var existing = FindRecord(Records, deviceId);
            if (existing == null)
                return;

            Replace(deviceId, new PeerConfiguration
            {
                DeviceId = existing.DeviceId,
                DeviceName = string.IsNullOrWhiteSpace(deviceName) ? existing.DeviceName : deviceName,
                Alias = existing.Alias,
                Addresses = WithAddress(existing.Addresses, address),
                ManualAddresses = existing.ManualAddresses,
                LastSeen = seenAt,
                PairedAt = existing.PairedAt,
                SharedDatabases = existing.SharedDatabases,
                DatabaseAliases = existing.DatabaseAliases,
            });
        }

        await _configurationService.SaveAsync(cancellationToken);
        PeersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task SetAliasAsync(DeviceId deviceId, string? alias, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var existing = FindRecord(Records, deviceId);
            if (existing == null)
                return;

            Replace(deviceId, new PeerConfiguration
            {
                DeviceId = existing.DeviceId,
                DeviceName = existing.DeviceName,
                Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim(),
                Addresses = existing.Addresses.ToList(),
                ManualAddresses = existing.ManualAddresses,
                LastSeen = existing.LastSeen,
                PairedAt = existing.PairedAt,
                SharedDatabases = existing.SharedDatabases,
                DatabaseAliases = existing.DatabaseAliases,
            });
        }

        await _configurationService.SaveAsync(cancellationToken);
        PeersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task RecordSharedDatabasesAsync(DeviceId deviceId, IReadOnlyList<SharedDatabase> sharedDatabases,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sharedDatabases);
        lock (_lock)
        {
            var existing = FindRecord(Records, deviceId);
            if (existing == null)
                return;

            Replace(deviceId, new PeerConfiguration
            {
                DeviceId = existing.DeviceId,
                DeviceName = existing.DeviceName,
                Alias = existing.Alias,
                Addresses = existing.Addresses.ToList(),
                ManualAddresses = existing.ManualAddresses,
                LastSeen = existing.LastSeen,
                PairedAt = existing.PairedAt,
                SharedDatabases = sharedDatabases
                    .Select(p => new PeerSharedDatabaseConfiguration { DatabaseId = p.DatabaseId.ToString("D"), Name = p.Name })
                    .ToList(),

                // Keyed by Database ID apart from the share list, so the Peer's report never drops them
                DatabaseAliases = existing.DatabaseAliases,
            });
        }

        await _configurationService.SaveAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetDatabaseAliasAsync(DeviceId deviceId, Guid databaseId, string? alias,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var existing = FindRecord(Records, deviceId);
            if (existing == null)
                return;

            var key = databaseId.ToString("D");
            var aliases = existing.DatabaseAliases
                .Where(p => !Guid.TryParse(p.DatabaseId, out var id) || id != databaseId)
                .ToList();
            if (!string.IsNullOrWhiteSpace(alias))
                aliases.Add(new PeerDatabaseAliasConfiguration { DatabaseId = key, Alias = alias.Trim() });

            Replace(deviceId, new PeerConfiguration
            {
                DeviceId = existing.DeviceId,
                DeviceName = existing.DeviceName,
                Alias = existing.Alias,
                Addresses = existing.Addresses.ToList(),
                ManualAddresses = existing.ManualAddresses,
                LastSeen = existing.LastSeen,
                PairedAt = existing.PairedAt,
                SharedDatabases = existing.SharedDatabases,
                DatabaseAliases = aliases,
            });
        }

        await _configurationService.SaveAsync(cancellationToken);
        PeersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (FindRecord(Records, deviceId) == null)
                return false;

            _configurationService.Configuration.Peers = Records.Where(p => !Matches(p, deviceId)).ToList();
        }

        _logger.LogInformation("Unpaired Device {DeviceId}", deviceId.ToDisplayString());
        PeerRemoved?.Invoke(this, new PeerRemovedEventArgs(deviceId));
        PeersChanged?.Invoke(this, EventArgs.Empty);
        await _configurationService.SaveAsync(cancellationToken);
        return true;
    }

    private List<PeerConfiguration> Records => _configurationService.Configuration.Peers;

    private void Replace(DeviceId deviceId, PeerConfiguration record)
    {
        var records = Records.Where(p => !Matches(p, deviceId)).ToList();
        records.Add(record);
        _configurationService.Configuration.Peers = records;
    }

    private static PeerConfiguration? FindRecord(List<PeerConfiguration> records, DeviceId deviceId) =>
        records.FirstOrDefault(p => Matches(p, deviceId));

    private static bool Matches(PeerConfiguration record, DeviceId deviceId) =>
        DeviceId.TryParse(record.DeviceId, out var parsed) && parsed == deviceId;

    private static List<string> WithAddress(IEnumerable<string> addresses, string? address)
    {
        var result = new List<string>(_maxAddresses);
        if (!string.IsNullOrWhiteSpace(address))
            result.Add(address);

        foreach (var item in addresses)
        {
            if (result.Count >= _maxAddresses)
                break;

            if (!string.IsNullOrWhiteSpace(item) && !result.Contains(item, StringComparer.OrdinalIgnoreCase))
                result.Add(item);
        }

        return result;
    }

    private static List<string> WithManualAddress(IEnumerable<string> addresses, string address)
    {
        var result = addresses.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (!result.Contains(address, StringComparer.OrdinalIgnoreCase))
            result.Add(address);

        return result;
    }

    /// <summary>
    /// The addresses to try for a Peer: its most recent ones, then any entered address that is not among them.
    /// </summary>
    private static List<string> GetAddresses(PeerConfiguration record)
    {
        var result = record.Addresses.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        foreach (var item in record.ManualAddresses)
        {
            if (!string.IsNullOrWhiteSpace(item) && !result.Contains(item, StringComparer.OrdinalIgnoreCase))
                result.Add(item);
        }

        return result;
    }

    private Peer? ToPeer(PeerConfiguration record)
    {
        if (!DeviceId.TryParse(record.DeviceId, out var deviceId))
        {
            _logger.LogWarning("Ignoring a Peer record with an invalid Device ID in clipmate.toml");
            return null;
        }

        var sharedDatabases = record.SharedDatabases
            .Where(p => Guid.TryParse(p.DatabaseId, out _))
            .Select(p => new SharedDatabase(Guid.Parse(p.DatabaseId), p.Name))
            .ToList();

        return new Peer(deviceId.Value, record.DeviceName, string.IsNullOrWhiteSpace(record.Alias) ? null : record.Alias,
            GetAddresses(record), record.LastSeen)
        {
            PairedAt = record.PairedAt == default ? null : record.PairedAt,
            SharedDatabases = sharedDatabases,
            DatabaseAliases = ToDatabaseAliases(record.DatabaseAliases),
        };
    }

    private static Dictionary<Guid, string> ToDatabaseAliases(IEnumerable<PeerDatabaseAliasConfiguration> records)
    {
        var aliases = new Dictionary<Guid, string>();
        foreach (var item in records)
        {
            if (Guid.TryParse(item.DatabaseId, out var databaseId) && !string.IsNullOrWhiteSpace(item.Alias))
                aliases[databaseId] = item.Alias;
        }

        return aliases;
    }
}
