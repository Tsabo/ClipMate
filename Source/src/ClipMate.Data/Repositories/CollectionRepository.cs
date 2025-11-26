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
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        _context.Collections.Add(collection);
        await _context.SaveChangesAsync(cancellationToken);
        return collection;
    }

    public async Task<bool> UpdateAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        _context.Collections.Update(collection);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections.FindAsync(new object[] { id }, cancellationToken);
        if (collection == null)
            return false;

        _context.Collections.Remove(collection);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
