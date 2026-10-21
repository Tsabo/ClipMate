using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
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
    public async Task SavePairedAsync(DeviceId deviceId, string deviceName, string? alias, string? address,
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
                LastSeen = existing?.LastSeen,
                PairedAt = DateTime.Now,
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
                LastSeen = seenAt,
                PairedAt = existing.PairedAt,
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
                LastSeen = existing.LastSeen,
                PairedAt = existing.PairedAt,
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

    private Peer? ToPeer(PeerConfiguration record)
    {
        if (!DeviceId.TryParse(record.DeviceId, out var deviceId))
        {
            _logger.LogWarning("Ignoring a Peer record with an invalid Device ID in clipmate.toml");
            return null;
        }

        return new Peer(deviceId.Value, record.DeviceName, string.IsNullOrWhiteSpace(record.Alias) ? null : record.Alias,
            record.Addresses.ToList(), record.LastSeen);
    }
}
