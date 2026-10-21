using System.Runtime.CompilerServices;
using ClipMate.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClipMate.Data;

/// <summary>
/// What a saved write changed in one database file: the collections and folders whose clips or properties changed.
/// </summary>
/// <param name="databasePath">The database file.</param>
/// <param name="collectionIds">Collections and folders known to have changed.</param>
/// <param name="clipIds">Clips whose content changed without their clip row being written; their collection is not known here.</param>
/// <param name="clipsChanged">Whether any clip (or its content) changed, which can change any Smart or SQL-based collection.</param>
public sealed class DatabaseChangedEventArgs(string databasePath, IReadOnlySet<Guid> collectionIds, IReadOnlySet<Guid> clipIds, bool clipsChanged)
    : EventArgs
{
    /// <summary>
    /// Gets the database file.
    /// </summary>
    public string DatabasePath { get; } = databasePath;

    /// <summary>
    /// Gets the collections and folders known to have changed.
    /// </summary>
    public IReadOnlySet<Guid> CollectionIds { get; } = collectionIds;

    /// <summary>
    /// Gets the clips whose content changed without their clip row being written.
    /// </summary>
    public IReadOnlySet<Guid> ClipIds { get; } = clipIds;

    /// <summary>
    /// Gets whether any clip (or its content) changed.
    /// </summary>
    public bool ClipsChanged { get; } = clipsChanged;
}

/// <summary>
/// The single place that sees every write to a ClipMate database: an EF Core interceptor attached to every context
/// <see cref="DatabaseContextFactory" /> creates, so captures, deletes, moves, edits and collection changes are all
/// noticed however they were made. Raises <see cref="DatabaseChanged" /> after a save succeeds. Does nothing while no
/// one listens.
/// </summary>
public sealed class DatabaseChangeMonitor : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, DatabaseChangedEventArgs> _pending = new();

    /// <summary>
    /// Raised (on the saving thread) after a save that changed clips or collections.
    /// </summary>
    public event EventHandler<DatabaseChangedEventArgs>? DatabaseChanged;

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Publish(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Publish(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData.Context);

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void SaveChangesCanceled(DbContextEventData eventData) => Discard(eventData.Context);

    /// <inheritdoc />
    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context == null || DatabaseChanged == null)
            return;

        var collectionIds = new HashSet<Guid>();
        var contentClipIds = new HashSet<Guid>();
        var clipIds = new HashSet<Guid>();
        var clipsChanged = false;

        // DetectChanges has already run when SavingChanges is called
        foreach (var item in context.ChangeTracker.Entries())
        {
            if (item.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            switch (item.Entity)
            {
                case Clip clip:
                    clipsChanged = true;
                    clipIds.Add(clip.Id);
                    AddIds(item, nameof(Clip.CollectionId), collectionIds);
                    AddIds(item, nameof(Clip.FolderId), collectionIds);
                    break;
                case Collection collection:
                    collectionIds.Add(collection.Id);
                    break;
                case ClipData format:
                    clipsChanged = true;
                    contentClipIds.Add(format.ClipId);
                    break;
                case BlobTxt blob:
                    clipsChanged = true;
                    contentClipIds.Add(blob.ClipId);
                    break;
                case BlobJpg blob:
                    clipsChanged = true;
                    contentClipIds.Add(blob.ClipId);
                    break;
                case BlobPng blob:
                    clipsChanged = true;
                    contentClipIds.Add(blob.ClipId);
                    break;
                case BlobBlob blob:
                    clipsChanged = true;
                    contentClipIds.Add(blob.ClipId);
                    break;
            }
        }

        contentClipIds.ExceptWith(clipIds);
        contentClipIds.Remove(Guid.Empty);
        collectionIds.Remove(Guid.Empty);
        if (collectionIds.Count == 0 && contentClipIds.Count == 0 && !clipsChanged)
            return;

        var path = context.Database.GetDbConnection().DataSource;
        if (string.IsNullOrEmpty(path))
            return;

        _pending.AddOrUpdate(context, new DatabaseChangedEventArgs(path, collectionIds, contentClipIds, clipsChanged));
    }

    private void Publish(DbContext? context)
    {
        if (context == null || !_pending.TryGetValue(context, out var change))
            return;

        _pending.Remove(context);
        DatabaseChanged?.Invoke(this, change);
    }

    private void Discard(DbContext? context)
    {
        if (context != null)
            _pending.Remove(context);
    }

    /// <summary>
    /// Adds a Guid property's current value and, for a change, its original value (a move changes both collections).
    /// </summary>
    private static void AddIds(EntityEntry entry, string propertyName, HashSet<Guid> ids)
    {
        var property = entry.Property(propertyName);
        if (property.CurrentValue is Guid current)
            ids.Add(current);

        if (entry.State == EntityState.Modified && property.OriginalValue is Guid original)
            ids.Add(original);
    }
}
