using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of the search query repository.
/// </summary>
public class SearchQueryRepository : ISearchQueryRepository
{
    private readonly ClipMateDbContext _context;

    public SearchQueryRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SearchQuery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SearchQueries
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SearchQuery>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SearchQueries
            .OrderByDescending(p => p.LastExecutedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<SearchQuery> CreateAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        _context.SearchQueries.Add(query);
        await _context.SaveChangesAsync(cancellationToken);
        return query;
    }

    public async Task<bool> UpdateAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        _context.SearchQueries.Update(query);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await _context.SearchQueries.FindAsync([id], cancellationToken);
        if (query == null)
        {
            return false;
        }

        _context.SearchQueries.Remove(query);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
