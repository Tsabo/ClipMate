using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Repository for managing User entities.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ClipMateDbContext _context;

    public UserRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<User?> GetByUsernameAndWorkstationAsync(string username, string workstation, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(p => p.Username == username && p.Workstation == workstation, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .OrderBy(p => p.Username)
            .ThenBy(p => p.Workstation)
            .ToListAsync(cancellationToken);
    }

    public async Task<User> CreateOrUpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        // Try to find existing user
        var existing = await GetByUsernameAndWorkstationAsync(user.Username, user.Workstation, cancellationToken);

        if (existing != null)
        {
            // Update last activity
            existing.LastDate = user.LastDate;
            _context.Users.Update(existing);
            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        // Create new user
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> UpdateLastActivityAsync(Guid id, DateTime lastDate, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);
        if (user == null)
            return false;

        user.LastDate = lastDate;
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);
        if (user == null)
            return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
