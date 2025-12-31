using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing clip shortcuts (nicknames for PowerPaste).
/// Registered as singleton to support multi-database operations.
/// Creates fresh DbContext instances for each operation (thread-safe).
/// </summary>
public class ShortcutService : IShortcutService
{
    private readonly IDatabaseManager _databaseManager;
    private readonly ILogger<ShortcutService> _logger;

    public ShortcutService(IDatabaseManager databaseManager, ILogger<ShortcutService> logger)
    {
        _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Shortcut>> GetAllAsync(string databaseKey, CancellationToken cancellationToken = default)
    {
        await using var context = _databaseManager.CreateDatabaseContext(databaseKey);
        if (context == null)
            throw new InvalidOperationException($"Database '{databaseKey}' is not loaded");

        return await context.Shortcuts
            .OrderBy(p => p.Nickname)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(string DatabaseKey, Shortcut Shortcut)>> GetAllFromAllDatabasesAsync(CancellationToken cancellationToken = default)
    {
        var allShortcuts = new List<(string DatabaseKey, Shortcut Shortcut)>();

        // Query each loaded database
        foreach (var (databaseKey, context) in _databaseManager.CreateAllDatabaseContexts())
        {
            await using (context)
            {
                try
                {
                    var shortcuts = await context.Shortcuts
                        .ToListAsync(cancellationToken);

                    // Add each shortcut with its database key
                    allShortcuts.AddRange(shortcuts.Select(item => (databaseKey, item)));
                }
                catch (Exception ex) when (ex.Message.Contains("no such table"))
                {
                    // ShortCut table doesn't exist in this database - skip it
                    _logger.LogDebug("ShortCut table not found in database {DatabaseKey}, skipping", databaseKey);
                }
            }
        }

        return allShortcuts.OrderBy(p => p.Shortcut.Nickname).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Shortcut>> GetByNicknamePrefixAsync(string databaseKey, string nicknamePrefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(nicknamePrefix))
            return Array.Empty<Shortcut>();

        await using var context = _databaseManager.CreateDatabaseContext(databaseKey);
        if (context == null)
            throw new InvalidOperationException($"Database '{databaseKey}' is not loaded");

        // Case-insensitive prefix match
        var prefixLower = nicknamePrefix.ToLowerInvariant();

        return await context.Shortcuts
            .Where(p => p.Nickname.ToLower().StartsWith(prefixLower))
            .OrderBy(p => p.Nickname)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(string DatabaseKey, Shortcut Shortcut)>> GetByNicknamePrefixFromAllDatabasesAsync(string nicknamePrefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(nicknamePrefix))
            return Array.Empty<(string, Shortcut)>();

        var allShortcuts = new List<(string DatabaseKey, Shortcut Shortcut)>();
        var prefixLower = nicknamePrefix.ToLowerInvariant();

        // Query each loaded database
        foreach (var (databaseKey, context) in _databaseManager.CreateAllDatabaseContexts())
        {
            await using (context)
            {
                try
                {
                    var shortcuts = await context.Shortcuts
                        .Where(p => p.Nickname.ToLower().StartsWith(prefixLower))
                        .ToListAsync(cancellationToken);

                    // Add each shortcut with its database key
                    allShortcuts.AddRange(shortcuts.Select(shortcut => (databaseKey, shortcut)));
                }
                catch (Exception ex) when (ex.Message.Contains("no such table"))
                {
                    // ShortCut table doesn't exist in this database - skip it
                    _logger.LogDebug("ShortCut table not found in database {DatabaseKey}, skipping", databaseKey);
                }
            }
        }

        return allShortcuts.OrderBy(p => p.Shortcut.Nickname).ToList();
    }

    /// <inheritdoc />
    public async Task<Shortcut?> GetByClipIdAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default)
    {
        await using var context = _databaseManager.CreateDatabaseContext(databaseKey);
        if (context == null)
            throw new InvalidOperationException($"Database '{databaseKey}' is not loaded");

        return await context.Shortcuts
            .FirstOrDefaultAsync(p => p.ClipId == clipId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateClipShortcutAsync(string databaseKey, Guid clipId, string? nickname, string? title = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("UpdateClipShortcutAsync called: DatabaseKey='{DatabaseKey}', ClipId={ClipId}, Nickname='{Nickname}', Title='{Title}'", databaseKey, clipId, nickname, title);

        await using var context = _databaseManager.CreateDatabaseContext(databaseKey);
        if (context == null)
            throw new InvalidOperationException($"Database '{databaseKey}' is not loaded");

        _logger.LogInformation("Got context for database '{DatabaseKey}', Database path: {DatabasePath}",
            databaseKey, context.Database.GetDbConnection().DataSource);

        // Validate nickname length
        if (nickname is { Length: > 64 })
            throw new ArgumentException("Shortcut nickname cannot exceed 64 characters", nameof(nickname));

        // Get existing shortcut
        Shortcut? existing;
        try
        {
            existing = await context.Shortcuts
                .FirstOrDefaultAsync(p => p.ClipId == clipId, cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("no such table"))
        {
            // ShortCut table doesn't exist - create it
            _logger.LogInformation("ShortCut table not found in database {DatabaseKey}, creating schema", databaseKey);
            await context.Database.EnsureCreatedAsync(cancellationToken);

            // Retry after ensuring schema
            existing = await context.Shortcuts
                .FirstOrDefaultAsync(p => p.ClipId == clipId, cancellationToken);
        }

        // Delete if nickname is null/empty
        if (string.IsNullOrWhiteSpace(nickname))
        {
            if (existing == null)
                return;

            context.Shortcuts.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deleted shortcut for clip {ClipId} in database {DatabaseKey}", clipId, databaseKey);

            return;
        }

        // Get the clip to retrieve ClipGuid for denormalization
        var clip = await context.Clips
            .FirstOrDefaultAsync(p => p.Id == clipId, cancellationToken);

        if (clip == null)
        {
            // Log all clips in this database to help diagnose
            var clipCount = await context.Clips.CountAsync(cancellationToken);
            _logger.LogError("Clip {ClipId} not found in database {DatabaseKey}. Database contains {ClipCount} clips. " +
                             "This usually means: 1) Clip was loaded from search results spanning multiple databases, " +
                             "2) Clip belongs to a different collection/database, or 3) Clip was deleted.",
                clipId, databaseKey, clipCount);

            throw new InvalidOperationException($"Clip {clipId} not found in database {databaseKey}. Please ensure the clip exists and you're using the correct database.");
        }

        // Update clip title if provided
        if (!string.IsNullOrWhiteSpace(title))
        {
            clip.Title = title;
            clip.CustomTitle = true;
            _logger.LogInformation("Updated clip {ClipId} title to '{Title}' in database {DatabaseKey}", clipId, title, databaseKey);
        }

        // Update or create
        if (existing != null)
        {
            existing.Nickname = nickname;
            existing.ClipGuid = clip.Id; // Update denormalized GUID
            _logger.LogInformation("Updated shortcut for clip {ClipId} to '{Nickname}' in database {DatabaseKey}", clipId, nickname, databaseKey);
        }
        else
        {
            var shortcut = new Shortcut
            {
                Id = Guid.NewGuid(),
                ClipId = clipId,
                Nickname = nickname,
                ClipGuid = clip.Id,
            };

            context.Shortcuts.Add(shortcut);
            _logger.LogInformation("Created shortcut '{Nickname}' for clip {ClipId} in database {DatabaseKey}", nickname, clipId, databaseKey);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string databaseKey, Guid shortcutId, CancellationToken cancellationToken = default)
    {
        await using var context = _databaseManager.CreateDatabaseContext(databaseKey);
        if (context == null)
            throw new InvalidOperationException($"Database '{databaseKey}' is not loaded");

        var shortcut = await context.Shortcuts.FindAsync([shortcutId], cancellationToken);
        if (shortcut != null)
        {
            context.Shortcuts.Remove(shortcut);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deleted shortcut {ShortcutId} in database {DatabaseKey}", shortcutId, databaseKey);
        }
    }

    /// <inheritdoc />
    public async Task DeleteByClipIdAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default)
    {
        await using var context = _databaseManager.CreateDatabaseContext(databaseKey);
        if (context == null)
            throw new InvalidOperationException($"Database '{databaseKey}' is not loaded");

        var shortcuts = await context.Shortcuts
            .Where(p => p.ClipId == clipId)
            .ToListAsync(cancellationToken);

        if (shortcuts.Count > 0)
        {
            context.Shortcuts.RemoveRange(shortcuts);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deleted {Count} shortcut(s) for clip {ClipId} in database {DatabaseKey}", shortcuts.Count, clipId, databaseKey);
        }
    }
}
