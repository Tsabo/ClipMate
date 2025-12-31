using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Repository for managing Monaco Editor state persistence.
/// </summary>
internal class MonacoEditorStateRepository : IMonacoEditorStateRepository
{
    private readonly ClipMateDbContext _context;

    public MonacoEditorStateRepository(ClipMateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MonacoEditorState?> GetByClipDataIdAsync(Guid clipDataId, CancellationToken cancellationToken = default)
    {
        return await _context.MonacoEditorStates
            .FirstOrDefaultAsync(p => p.ClipDataId == clipDataId, cancellationToken);
    }

    public async Task UpsertAsync(MonacoEditorState state, CancellationToken cancellationToken = default)
    {
        var existing = await GetByClipDataIdAsync(state.ClipDataId, cancellationToken);

        if (existing != null)
        {
            // Update existing state
            existing.Language = state.Language;
            existing.ViewState = state.ViewState;
            existing.LastModified = DateTime.UtcNow;
        }
        else
        {
            // Create new state
            state.Id = Guid.NewGuid();
            state.LastModified = DateTime.UtcNow;
            _context.MonacoEditorStates.Add(state);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByClipDataIdAsync(Guid clipDataId, CancellationToken cancellationToken = default)
    {
        var state = await GetByClipDataIdAsync(clipDataId, cancellationToken);
        if (state != null)
        {
            _context.MonacoEditorStates.Remove(state);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
