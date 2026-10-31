using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Telling local and Remote Databases apart, in <see cref="Databases" /> only.
/// </summary>
public class DatabasesTests
{
    private static readonly RemoteDatabaseAddress _address = new(DeviceId.FromCertificateBytes([1]), Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private ClipMateConfiguration _configuration = null!;
    private IConfigurationService _configurationService = null!;
    private Databases _databases = null!;
    private string _databaseFile = null!;
    private IRemoteDatabaseService _remoteDatabases = null!;
    private RemoteDatabase _laptopClips = null!;
    private IDatabase _laptopDatabase = null!;

    [Before(Test)]
    public void Setup()
    {
        _databaseFile = Path.GetTempFileName();
        _configuration = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                ["db_main"] = new() { Name = "My Clips", FilePath = _databaseFile },
                ["db_archive"] = new() { Name = "Archive", FilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db") },
            },
        };

        _configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => _configurationService.Configuration).Returns(_configuration);

        _laptopClips = new RemoteDatabase(_address, "Clips", "LAPTOP", PeerConnectionStatus.Online, null);
        _laptopDatabase = A.Fake<IDatabase>();
        _remoteDatabases = A.Fake<IRemoteDatabaseService>();
        A.CallTo(() => _remoteDatabases.GetRemoteDatabases()).Returns([_laptopClips]);
        A.CallTo(() => _remoteDatabases.Open(_laptopClips)).Returns(_laptopDatabase);

        _databases = new Databases(_configurationService, _remoteDatabases, A.Fake<IDatabaseContextFactory>(), A.Fake<ISearchService>());
    }

    [After(Test)]
    public void Cleanup()
    {
        _databases.Dispose();
        File.Delete(_databaseFile);
    }

    [Test]
    public async Task All_ListsTheLocalDatabasesInConfiguredOrderThenTheRemoteOnes()
    {
        // Act
        var all = _databases.All;

        // Assert
        await Assert.That(all.Count).IsEqualTo(3);
        await Assert.That(all[0].Key).IsEqualTo("db_main");
        await Assert.That(all[1].Key).IsEqualTo("db_archive");
        await Assert.That(all[0].Name).IsEqualTo("My Clips");
        await Assert.That(all[0].IsAvailable).IsTrue();
        await Assert.That(all[1].IsAvailable).IsFalse();
        await Assert.That(all[2]).IsSameReferenceAs(_laptopDatabase);
    }

    [Test]
    public async Task Get_LocalKey_IsThatConfiguredDatabase()
    {
        // Act
        var database = _databases.Get("db_main");

        // Assert
        await Assert.That(database.Key).IsEqualTo("db_main");
        await Assert.That(database.Remote).IsNull();
        await Assert.That(database.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task Get_RemoteKey_IsThatRemoteDatabase()
    {
        // Act
        var database = _databases.Get(RemoteDatabaseKey.From(_address));

        // Assert
        await Assert.That(database).IsSameReferenceAs(_laptopDatabase);
    }

    [Test]
    public async Task Get_RemoteDatabaseNoLongerShared_IsUnavailable()
    {
        // Arrange
        var unshared = RemoteDatabaseKey.From(new RemoteDatabaseAddress(_address.DeviceId, Guid.NewGuid()));

        // Act & Assert
        await Assert.That(() => _databases.Get(unshared)).Throws<DatabaseUnavailableException>();
    }

    [Test]
    public async Task Get_LocalDatabaseNoLongerConfigured_IsUnavailable()
    {
        // Act & Assert
        await Assert.That(() => _databases.Get("db_removed")).Throws<DatabaseUnavailableException>();
    }

    [Test]
    public async Task Changed_SettingsSavedOrRemoteDatabasesChanged_IsRaised()
    {
        // Arrange
        var raised = 0;
        _databases.Changed += (_, _) => raised++;

        // Act
        _configurationService.Saved += Raise.WithEmpty();
        _remoteDatabases.RemoteDatabasesChanged += Raise.WithEmpty();

        // Assert
        await Assert.That(raised).IsEqualTo(2);
    }
}
