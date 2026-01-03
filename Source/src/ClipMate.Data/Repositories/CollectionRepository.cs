using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of the collection repository.
/// </summary>
public class CollectionRepository : ICollectionRepository
{
    private readonly ClipMateDbContext _context;

    public CollectionRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Collection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Collections
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Collections
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Collection?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Collections
            .FirstOrDefaultAsync(p => p.IsActive, cancellationToken);
    }

    public async Task<Collection> CreateAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        // Validate unique role constraint for special roles
        await ValidateUniqueRoleAsync(collection.Role, null, cancellationToken);

        _context.Collections.Add(collection);
        await _context.SaveChangesAsync(cancellationToken);
        return collection;
    }

    public async Task<bool> UpdateAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        // Validate unique role constraint for special roles (exclude current collection)
        await ValidateUniqueRoleAsync(collection.Role, collection.Id, cancellationToken);

        _context.Collections.Update(collection);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections.FindAsync([id], cancellationToken);
        if (collection == null)
            return false;

        _context.Collections.Remove(collection);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public Task<Collection?> GetByIdAsync(string databaseKey, Guid collectionId)
    {
        // TODO: Implement with proper database key handling
        return GetByIdAsync(collectionId);
    }

    /// <inheritdoc />
    public async Task<Collection> GetOverflowCollectionAsync(string databaseKey)
    {
        // TODO: Implement with proper database key handling and creation logic
        var overflow = await _context.Collections
            .FirstOrDefaultAsync(p => p.Role == CollectionRole.Overflow);

        if (overflow != null)
            return overflow;

        overflow = new Collection
        {
            Id = Guid.NewGuid(),
            Title = "Overflow",
            LmType = CollectionLmType.Normal,
            Role = CollectionRole.Overflow,
            CreatedAt = DateTime.UtcNow,
        };

        await _context.Collections.AddAsync(overflow);
        await _context.SaveChangesAsync();

        return overflow;
    }

    /// <inheritdoc />
    public async Task<Collection> GetTrashcanCollectionAsync(string databaseKey)
    {
        // TODO: Implement with proper database key handling
        var trashcan = await _context.Collections
            .FirstOrDefaultAsync(p => p.Role == CollectionRole.Trashcan);

        return trashcan ?? throw new InvalidOperationException("Trashcan collection not found in database.");
    }

    /// <summary>
    /// Validates that special roles (Inbox, Overflow, Trashcan) are unique within the database.
    /// Role.None is allowed to be non-unique as it represents regular collections.
    /// </summary>
    /// <param name="role">The role to validate.</param>
    /// <param name="excludeCollectionId">Collection ID to exclude from the check (for updates).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate special role is detected.</exception>
    private async Task ValidateUniqueRoleAsync(CollectionRole role, Guid? excludeCollectionId, CancellationToken cancellationToken)
    {
        // Role.None is not unique - skip validation
        if (role == CollectionRole.None)
            return;

        // Check if another collection already has this special role
        var existingCollection = await _context.Collections
            .Where(c => c.Role == role)
            .Where(c => excludeCollectionId == null || c.Id != excludeCollectionId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingCollection != null)
        {
            throw new InvalidOperationException(
                $"A collection with role '{role}' already exists (ID: {existingCollection.Id}, Title: '{existingCollection.Title}'). "
                + "Special roles (Inbox, Overflow, Trashcan) must be unique within a database.");
        }
    }
}
