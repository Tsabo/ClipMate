using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of the sound event repository.
/// </summary>
public class SoundEventRepository : ISoundEventRepository
{
    private readonly ClipMateDbContext _context;

    public SoundEventRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SoundEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SoundEvents
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SoundEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SoundEvents
            .OrderBy(p => p.EventType)
            .ToListAsync(cancellationToken);
    }

    public async Task<SoundEvent?> GetByEventTypeAsync(SoundEventType eventType, CancellationToken cancellationToken = default)
    {
        return await _context.SoundEvents
            .FirstOrDefaultAsync(p => p.EventType == eventType, cancellationToken);
    }

    public async Task<SoundEvent> CreateAsync(SoundEvent soundEvent, CancellationToken cancellationToken = default)
    {
        if (soundEvent == null)
            throw new ArgumentNullException(nameof(soundEvent));

        _context.SoundEvents.Add(soundEvent);
        await _context.SaveChangesAsync(cancellationToken);
        return soundEvent;
    }

    public async Task<bool> UpdateAsync(SoundEvent soundEvent, CancellationToken cancellationToken = default)
    {
        if (soundEvent == null)
            throw new ArgumentNullException(nameof(soundEvent));

        _context.SoundEvents.Update(soundEvent);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var soundEvent = await _context.SoundEvents.FindAsync([id], cancellationToken);
        if (soundEvent == null)
            return false;

        _context.SoundEvents.Remove(soundEvent);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
