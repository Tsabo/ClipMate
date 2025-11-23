using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Repository for managing Shortcut entities (PowerPaste nicknames).
/// </summary>
public class ShortcutRepository : IShortcutRepository
{
    private readonly ClipMateDbContext _context;

    public ShortcutRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Shortcut?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Shortcuts
            .Include(s => s.Clip)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Shortcut?> GetByNicknameAsync(string nickname, CancellationToken cancellationToken = default)
    {
        return await _context.Shortcuts
            .Include(s => s.Clip)
            .FirstOrDefaultAsync(s => s.Nickname == nickname, cancellationToken);
    }

    public async Task<IReadOnlyList<Shortcut>> GetByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        return await _context.Shortcuts
            .Where(s => s.ClipId == clipId)
            .OrderBy(s => s.Nickname)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Shortcut>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Shortcuts
            .OrderBy(s => s.Nickname)
            .ToListAsync(cancellationToken);
    }

    public async Task<Shortcut> CreateAsync(Shortcut shortcut, CancellationToken cancellationToken = default)
    {
        // Ensure ClipGuid is set from ClipId if not already set
        if (shortcut.ClipGuid == Guid.Empty)
        {
            shortcut.ClipGuid = shortcut.ClipId;
        }

        _context.Shortcuts.Add(shortcut);
        await _context.SaveChangesAsync(cancellationToken);
        return shortcut;
    }

    public async Task<bool> UpdateAsync(Shortcut shortcut, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Shortcuts.FindAsync(new object[] { shortcut.Id }, cancellationToken);
        if (existing == null)
        {
            return false;
        }

        existing.Nickname = shortcut.Nickname;
        existing.ClipId = shortcut.ClipId;
        existing.ClipGuid = shortcut.ClipId; // Keep denormalized GUID in sync

        _context.Shortcuts.Update(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var shortcut = await _context.Shortcuts.FindAsync(new object[] { id }, cancellationToken);
        if (shortcut == null)
        {
            return false;
        }

        _context.Shortcuts.Remove(shortcut);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteByClipIdAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        var shortcuts = await _context.Shortcuts
            .Where(s => s.ClipId == clipId)
            .ToListAsync(cancellationToken);

        _context.Shortcuts.RemoveRange(shortcuts);
        await _context.SaveChangesAsync(cancellationToken);

        return shortcuts.Count;
    }

    public async Task<bool> NicknameExistsAsync(string nickname, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Shortcuts.Where(s => s.Nickname == nickname);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
