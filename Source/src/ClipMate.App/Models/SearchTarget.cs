using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.App.Models;

/// <summary>
/// What the search dialog searches, as chosen in "Search in": one Database (or a collection or folder in it), or every
/// Database at once. Each kind answers for itself what the dialog offers and what a search covers, so callers don't
/// tell them apart.
/// </summary>
public abstract record SearchTarget
{
    private protected SearchTarget()
    {
    }

    /// <summary>
    /// Every available Database, local and remote, searched at once; each one's matches go to its own Search Results
    /// node.
    /// </summary>
    public static AllDatabasesSearchTarget AllDatabases { get; } = new();

    /// <summary>
    /// Gets what the dialog calls it: "All Databases", or the Database's name as the collection tree shows it.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the local Database whose SQL the dialog validates and suggests, or <c>null</c> when the search sends no SQL:
    /// a Remote Database, or "All Databases", which includes them.
    /// </summary>
    public abstract string? SqlDatabaseKey { get; }

    /// <summary>
    /// Gets whether a local Database is searched, where the deleted-clips and shortcut filters apply.
    /// </summary>
    public abstract bool SearchesLocalDatabases { get; }

    /// <summary>
    /// Gets whether a Peer's Database may be searched, which never serves deleted clips or shortcuts.
    /// </summary>
    public abstract bool SearchesPeers { get; }

    /// <summary>
    /// The Databases searched, in tree order, whether or not they are available right now.
    /// </summary>
    /// <exception cref="DatabaseUnavailableException">The one Database chosen is no longer configured or shared.</exception>
    public abstract IReadOnlyList<IDatabase> DatabasesIn(IDatabases databases);

    /// <summary>
    /// <paramref name="filters" />, narrowed to the collection or folder chosen.
    /// </summary>
    public abstract SearchFilters Narrow(SearchFilters filters);

    /// <summary>
    /// Gets whether choosing <paramref name="other" /> in "Search in" is the same choice as this one.
    /// </summary>
    public abstract bool IsSameChoiceAs(SearchTarget? other);

    /// <summary>
    /// Gets whether <paramref name="other" /> searches the same Databases, whatever collection or folder it chose.
    /// </summary>
    public abstract bool SearchesSameDatabasesAs(SearchTarget? other);

    /// <summary>
    /// Describes the target for the log: the Database key and name, and the collection and folder chosen.
    /// </summary>
    public abstract string Describe();

    /// <summary>
    /// The target for a whole Database: a Remote one under its Peer's name.
    /// </summary>
    public static DatabaseSearchTarget For(IDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return new DatabaseSearchTarget(database.Key, database.Remote?.DisplayName ?? database.Name) { IsRemote = database.Remote != null };
    }
}

/// <summary>
/// One Database, local or remote, or a collection or folder in it (searched with the "current collection" and "specific
/// folder" scopes).
/// </summary>
/// <param name="DatabaseKey">The Database's key; a <see cref="RemoteDatabaseKey" /> for a Remote Database.</param>
/// <param name="DatabaseName">The Database's name as the collection tree shows it.</param>
public sealed record DatabaseSearchTarget(string DatabaseKey, string DatabaseName) : SearchTarget
{
    /// <summary>
    /// The chosen collection, or the collection of the chosen folder; <c>null</c> when none is chosen or it cannot be
    /// searched by membership (a Smart or SQL-based collection, the Trashcan, search results).
    /// </summary>
    public NamedEntry? Collection { get; init; }

    /// <summary>
    /// The chosen folder, or <c>null</c> when no folder is chosen.
    /// </summary>
    public NamedEntry? Folder { get; init; }

    /// <summary>
    /// Gets whether the target is a Remote Database: searched by its Peer, with no SQL. Set by whoever makes the
    /// target from a Remote Database's node.
    /// </summary>
    public bool IsRemote { get; init; }

    /// <inheritdoc />
    public override string Name => DatabaseName;

    /// <inheritdoc />
    public override string? SqlDatabaseKey => IsRemote ? null : DatabaseKey;

    /// <inheritdoc />
    public override bool SearchesLocalDatabases => !IsRemote;

    /// <inheritdoc />
    public override bool SearchesPeers => IsRemote;

    /// <summary>
    /// Gets the scope the target means: a folder, a collection, or all collections of its Database.
    /// </summary>
    public SearchScope Scope => this switch
    {
        { Folder: not null } => SearchScope.SpecificFolder,
        { Collection: not null } => SearchScope.CurrentCollection,
        var _ => SearchScope.AllCollections,
    };

    /// <inheritdoc />
    public override IReadOnlyList<IDatabase> DatabasesIn(IDatabases databases)
    {
        ArgumentNullException.ThrowIfNull(databases);
        return [databases.Get(DatabaseKey)];
    }

    /// <inheritdoc />
    public override SearchFilters Narrow(SearchFilters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var scope = Scope;
        return filters with
        {
            Scope = scope,
            CollectionId = scope == SearchScope.CurrentCollection ? Collection?.Id : null,
            FolderId = scope == SearchScope.SpecificFolder ? Folder?.Id : null,
        };
    }

    /// <summary>
    /// The same Database and the same folder, or with no folder the same collection. IDs alone never match, as
    /// Databases share collection IDs.
    /// </summary>
    public override bool IsSameChoiceAs(SearchTarget? other) =>
        other is DatabaseSearchTarget target && target.DatabaseKey == DatabaseKey && target.Folder?.Id == Folder?.Id &&
        (Folder != null || target.Collection?.Id == Collection?.Id);

    /// <inheritdoc />
    public override bool SearchesSameDatabasesAs(SearchTarget? other) => other is DatabaseSearchTarget target && target.DatabaseKey == DatabaseKey;

    /// <inheritdoc />
    public override string Describe()
    {
        var description = $"Database {DatabaseKey} ('{DatabaseName}'{(IsRemote ? ", remote" : string.Empty)})";
        if (Collection != null)
            description += $", collection '{Collection.Name}'";

        if (Folder != null)
            description += $", folder '{Folder.Name}'";

        return description;
    }
}

/// <summary>
/// Every available Database, local and remote, searched at once; each one's matches go to its own Search Results node.
/// Use <see cref="SearchTarget.AllDatabases" />.
/// </summary>
public sealed record AllDatabasesSearchTarget : SearchTarget
{
    /// <summary>
    /// The collection tree has no node for "All Databases", so it has an icon of its own.
    /// </summary>
    public const string Icon = "🗃️";

    internal AllDatabasesSearchTarget()
    {
    }

    /// <inheritdoc />
    public override string Name => "All Databases";

    /// <inheritdoc />
    public override string? SqlDatabaseKey => null;

    /// <inheritdoc />
    public override bool SearchesLocalDatabases => true;

    /// <inheritdoc />
    public override bool SearchesPeers => true;

    /// <inheritdoc />
    public override IReadOnlyList<IDatabase> DatabasesIn(IDatabases databases)
    {
        ArgumentNullException.ThrowIfNull(databases);
        return databases.All;
    }

    /// <inheritdoc />
    public override SearchFilters Narrow(SearchFilters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return filters with { Scope = SearchScope.AllCollections, CollectionId = null, FolderId = null };
    }

    /// <inheritdoc />
    public override bool IsSameChoiceAs(SearchTarget? other) => other is AllDatabasesSearchTarget;

    /// <inheritdoc />
    public override bool SearchesSameDatabasesAs(SearchTarget? other) => other is AllDatabasesSearchTarget;

    /// <inheritdoc />
    public override string Describe() => Name;
}

/// <summary>
/// A collection or folder in a Database's tree: its ID, and its name for the log.
/// </summary>
public sealed record NamedEntry(Guid Id, string Name);
