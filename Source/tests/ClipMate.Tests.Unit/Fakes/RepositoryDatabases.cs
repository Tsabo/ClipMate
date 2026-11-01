using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Networking.SharedDatabases;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Fakes;

/// <summary>
/// <see cref="IDatabases" /> for tests of services that read clips: every local database key is a real
/// <see cref="LocalDatabase" /> reading through the given context factory's repositories, and every Remote Database key
/// a real <see cref="RemoteDatabaseProxy" /> over the given <see cref="IRemoteDatabaseService" />, its Peer online.
/// </summary>
internal sealed class RepositoryDatabases(IDatabaseContextFactory factory, IRemoteDatabaseService? remoteDatabases = null) : IDatabases
{
    // Local Databases are available while their file exists; the contexts here are in memory or faked
    private static readonly string _existingFile = typeof(RepositoryDatabases).Assembly.Location;

    private readonly IRemoteDatabaseService _remoteDatabases = remoteDatabases ?? A.Fake<IRemoteDatabaseService>();

    /// <inheritdoc />
    public IReadOnlyList<IDatabase> All => [];

    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public IDatabase Get(string databaseKey) =>
        RemoteDatabaseKey.TryParse(databaseKey, out var address)
            ? new RemoteDatabaseProxy(new RemoteDatabase(address, "Remote", "Peer", PeerConnectionStatus.Online, null), _remoteDatabases)
            : new LocalDatabase(databaseKey, new DatabaseConfiguration { Name = databaseKey, FilePath = _existingFile }, factory, A.Fake<ISearchService>());
}
