using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

public class PeerStoreTests
{
    private static readonly DeviceId _laptop = DeviceId.FromCertificateBytes([1]);
    private static readonly DeviceId _desktop = DeviceId.FromCertificateBytes([2]);
    private string _directory = null!;

    [Before(Test)]
    public void Setup() => _directory = Path.Combine(Path.GetTempPath(), $"clipmate_peerstore_{Guid.NewGuid():N}");

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public async Task SavePairedAsync_PersistsThePeerAcrossRestarts()
    {
        // Arrange
        var store = await CreateStoreAsync();

        // Act
        await store.SavePairedAsync(_laptop, "LAPTOP-01", "My laptop", "192.168.1.20:47823");
        var reloaded = await CreateStoreAsync();

        // Assert
        var peer = reloaded.Find(_laptop);
        await Assert.That(peer).IsNotNull();
        await Assert.That(peer!.DeviceName).IsEqualTo("LAPTOP-01");
        await Assert.That(peer.Alias).IsEqualTo("My laptop");
        await Assert.That(peer.DisplayName).IsEqualTo("My laptop");
        await Assert.That(peer.Addresses).IsEquivalentTo(["192.168.1.20:47823"]);
        await Assert.That(reloaded.IsPaired(_laptop)).IsTrue();
    }

    [Test]
    public async Task RecordSharedDatabasesAsync_PersistsThePeersShareListAcrossRestarts()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, "192.168.1.20:47823");
        var work = new SharedDatabase(Guid.NewGuid(), "Work");

        // Act
        await store.RecordSharedDatabasesAsync(_laptop, [work]);
        var reloaded = await CreateStoreAsync();

        // Assert
        await Assert.That(reloaded.Find(_laptop)!.SharedDatabases).IsEquivalentTo([work]);
    }

    [Test]
    public async Task RecordSeenAsync_AfterRecordingSharedDatabases_KeepsThem()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, null);
        var work = new SharedDatabase(Guid.NewGuid(), "Work");
        await store.RecordSharedDatabasesAsync(_laptop, [work]);

        // Act
        await store.RecordSeenAsync(_laptop, "LAPTOP-02", "192.168.1.21:47823", DateTime.Now);
        await store.SavePairedAsync(_laptop, "LAPTOP-02", "Laptop", null);
        await store.SetAliasAsync(_laptop, "Travel laptop");

        // Assert
        await Assert.That(store.Find(_laptop)!.SharedDatabases).IsEquivalentTo([work]);
    }

    [Test]
    public async Task RecordSharedDatabasesAsync_DeviceNotPaired_RecordsNothing()
    {
        // Arrange
        var store = await CreateStoreAsync();

        // Act
        await store.RecordSharedDatabasesAsync(_laptop, [new SharedDatabase(Guid.NewGuid(), "Work")]);

        // Assert
        await Assert.That(store.GetPeers()).IsEmpty();
    }

    [Test]
    public async Task IsPaired_UnknownDevice_IsFalse()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, null);

        // Act / Assert
        await Assert.That(store.IsPaired(_desktop)).IsFalse();
        await Assert.That(store.Find(_desktop)).IsNull();
    }

    [Test]
    public async Task SavePairedAsync_WithoutAlias_ShowsTheDeviceName()
    {
        // Arrange
        var store = await CreateStoreAsync();

        // Act
        await store.SavePairedAsync(_laptop, "LAPTOP-01", "  ", null);

        // Assert
        var peer = store.Find(_laptop)!;
        await Assert.That(peer.Alias).IsNull();
        await Assert.That(peer.DisplayName).IsEqualTo("LAPTOP-01");
        await Assert.That(peer.LastSeen).IsNull();
    }

    [Test]
    public async Task SavePairedAsync_RaisesPeersChanged()
    {
        // Arrange
        var store = await CreateStoreAsync();
        var raised = 0;
        store.PeersChanged += (_, _) => raised++;

        // Act
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, null);

        // Assert
        await Assert.That(raised).IsEqualTo(1);
        await Assert.That(store.GetPeers().Select(p => p.DeviceId)).IsEquivalentTo([_laptop]);
    }

    [Test]
    public async Task RecordSeenAsync_UpdatesNameLastSeenAndPutsTheNewestAddressFirst()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, "192.168.1.20:47823");
        var seenAt = new DateTime(2026, 10, 20, 9, 30, 0);

        // Act
        await store.RecordSeenAsync(_laptop, "Travel laptop", "10.0.0.5:47823", seenAt);
        var reloaded = await CreateStoreAsync();

        // Assert
        var peer = reloaded.Find(_laptop)!;
        await Assert.That(peer.DeviceName).IsEqualTo("Travel laptop");
        await Assert.That(peer.LastSeen).IsEqualTo(seenAt);
        await Assert.That(peer.Addresses).IsEquivalentTo(["10.0.0.5:47823", "192.168.1.20:47823"]);
        await Assert.That(peer.Addresses[0]).IsEqualTo("10.0.0.5:47823");
    }

    [Test]
    public async Task RecordSeenAsync_UnpairedDevice_DoesNotAddIt()
    {
        // Arrange
        var store = await CreateStoreAsync();

        // Act
        await store.RecordSeenAsync(_desktop, "DESKTOP", "10.0.0.6:47823", DateTime.Now);

        // Assert
        await Assert.That(store.IsPaired(_desktop)).IsFalse();
        await Assert.That(store.GetPeers()).IsEmpty();
    }

    [Test]
    public async Task SetAliasAsync_ChangesOnlyTheLocalLabelAndPersists()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, "192.168.1.20:47823");

        // Act
        await store.SetAliasAsync(_laptop, "  Travel laptop ");
        var reloaded = await CreateStoreAsync();

        // Assert
        var peer = reloaded.Find(_laptop)!;
        await Assert.That(peer.Alias).IsEqualTo("Travel laptop");
        await Assert.That(peer.DisplayName).IsEqualTo("Travel laptop");
        await Assert.That(peer.DeviceName).IsEqualTo("LAPTOP-01");
        await Assert.That(peer.Addresses).IsEquivalentTo(["192.168.1.20:47823"]);
    }

    [Test]
    public async Task SetAliasAsync_Blank_ClearsTheAliasAndFollowsTheLiveDeviceName()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", "Travel laptop", null);

        // Act
        await store.SetAliasAsync(_laptop, " ");
        await store.RecordSeenAsync(_laptop, "LAPTOP-RENAMED", null, DateTime.Now);

        // Assert
        var peer = store.Find(_laptop)!;
        await Assert.That(peer.Alias).IsNull();
        await Assert.That(peer.DisplayName).IsEqualTo("LAPTOP-RENAMED");
    }

    [Test]
    public async Task SetAliasAsync_AliasesNeedNotBeUnique()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, null);
        await store.SavePairedAsync(_desktop, "DESKTOP", null, null);

        // Act
        await store.SetAliasAsync(_laptop, "Work");
        await store.SetAliasAsync(_desktop, "Work");

        // Assert
        await Assert.That(store.GetPeers().Select(p => p.DisplayName)).IsEquivalentTo(["Work", "Work"]);
    }

    [Test]
    public async Task SetAliasAsync_RaisesPeersChanged()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, null);
        var raised = 0;
        store.PeersChanged += (_, _) => raised++;

        // Act
        await store.SetAliasAsync(_laptop, "Travel laptop");

        // Assert
        await Assert.That(raised).IsEqualTo(1);
    }

    [Test]
    public async Task SetAliasAsync_UnpairedDevice_DoesNotAddIt()
    {
        // Arrange
        var store = await CreateStoreAsync();

        // Act
        await store.SetAliasAsync(_desktop, "Desk");

        // Assert
        await Assert.That(store.GetPeers()).IsEmpty();
    }

    [Test]
    public async Task RemoveAsync_UnpairsTheDeviceAcrossRestarts()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, null);
        await store.SavePairedAsync(_desktop, "DESKTOP", null, null);

        // Act
        var removed = await store.RemoveAsync(_laptop);
        var reloaded = await CreateStoreAsync();

        // Assert
        await Assert.That(removed).IsTrue();
        await Assert.That(store.IsPaired(_laptop)).IsFalse();
        await Assert.That(reloaded.IsPaired(_laptop)).IsFalse();
        await Assert.That(reloaded.GetPeers().Select(p => p.DeviceId)).IsEquivalentTo([_desktop]);
    }

    [Test]
    public async Task RemoveAsync_RaisesPeerRemovedAndPeersChanged()
    {
        // Arrange
        var store = await CreateStoreAsync();
        await store.SavePairedAsync(_laptop, "LAPTOP-01", null, null);
        DeviceId? removedId = null;
        var changed = 0;
        store.PeerRemoved += (_, e) => removedId = e.DeviceId;
        store.PeersChanged += (_, _) => changed++;

        // Act
        await store.RemoveAsync(_laptop);

        // Assert
        await Assert.That(removedId).IsEqualTo(_laptop);
        await Assert.That(changed).IsEqualTo(1);
    }

    [Test]
    public async Task RemoveAsync_UnpairedDevice_ReturnsFalseAndRaisesNothing()
    {
        // Arrange
        var store = await CreateStoreAsync();
        var raised = 0;
        store.PeerRemoved += (_, _) => raised++;
        store.PeersChanged += (_, _) => raised++;

        // Act
        var removed = await store.RemoveAsync(_laptop);

        // Assert
        await Assert.That(removed).IsFalse();
        await Assert.That(raised).IsEqualTo(0);
    }

    private async Task<PeerStore> CreateStoreAsync()
    {
        var configurationService = new ConfigurationService(_directory, NullLogger<ConfigurationService>.Instance);
        if (File.Exists(configurationService.ConfigurationFilePath))
            await configurationService.LoadAsync();
        else
        {
            configurationService.Configuration.Databases["db_test"] = new DatabaseConfiguration { Name = "Test", FilePath = "test.db" };
            await configurationService.SaveAsync();
        }

        return new PeerStore(configurationService, NullLogger<PeerStore>.Instance);
    }
}
