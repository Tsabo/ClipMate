using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of the application filter repository.
/// </summary>
public class ApplicationFilterRepository : IApplicationFilterRepository
{
    private readonly ClipMateDbContext _context;

    public ApplicationFilterRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ApplicationFilter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ApplicationFilters
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationFilter>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ApplicationFilters
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationFilter>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ApplicationFilters
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApplicationFilter> CreateAsync(ApplicationFilter filter, CancellationToken cancellationToken = default)
    {
        if (filter == null)
            throw new ArgumentNullException(nameof(filter));

        _context.ApplicationFilters.Add(filter);
        await _context.SaveChangesAsync(cancellationToken);
        return filter;
    }

    public async Task<bool> UpdateAsync(ApplicationFilter filter, CancellationToken cancellationToken = default)
    {
        if (filter == null)
            throw new ArgumentNullException(nameof(filter));

        _context.ApplicationFilters.Update(filter);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = await _context.ApplicationFilters.FindAsync([id], cancellationToken);
        if (filter == null)
            return false;

        _context.ApplicationFilters.Remove(filter);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
