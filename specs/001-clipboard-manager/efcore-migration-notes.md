# EF Core Migration Notes

**Date:** 2025-11-12  
**Migration:** LiteDB → Entity Framework Core 9.0.0 + SQLite

## Overview

Successfully migrated the data layer from LiteDB to Entity Framework Core based on:
- Team has "years of experience with Entity Framework"
- Implementation Policy requires Microsoft solutions first
- Need for proper abstraction layer and testability
- Built-in migration and schema versioning support

## Changes Made

### Packages
- ❌ Removed: `LiteDB` 5.0.21
- ✅ Added: `Microsoft.EntityFrameworkCore.Sqlite` 9.0.0
- ✅ Added: `Microsoft.EntityFrameworkCore.Design` 9.0.0
- ✅ Added: `Microsoft.EntityFrameworkCore.Tools` 9.0.0

### Files Created
1. `ClipMateDbContext.cs` - EF Core DbContext with entity configurations
2. `ClipMateDbContextFactory.cs` - Design-time factory for migrations
3. `Repositories/ClipRepository.cs` - EF Core implementation
4. `Repositories/CollectionRepository.cs` - EF Core implementation
5. `Repositories/FolderRepository.cs` - EF Core implementation
6. `Repositories/TemplateRepository.cs` - EF Core implementation
7. `Repositories/SearchQueryRepository.cs` - EF Core implementation
8. `Repositories/ApplicationFilterRepository.cs` - EF Core implementation
9. `Repositories/SoundEventRepository.cs` - EF Core implementation
10. `Migrations/20251112151350_InitialCreate.cs` - Initial schema migration
11. `Migrations/ClipMateDbContextModelSnapshot.cs` - EF Core model snapshot

### Files Deleted
- Entire `LiteDB/` folder (LiteDbContext, SchemaManager, BackupService, all 7 repositories)

### Files Updated
- `DependencyInjection/ServiceCollectionExtensions.cs` - Replaced LiteDB registration with EF Core DbContext
- `ClipMate.Data.csproj` - Updated package references
- `ClipMate.App.csproj` - Added EntityFrameworkCore.Design package
- `Directory.Packages.props` - Updated package versions

## Property Name Corrections

During migration, aligned property names between models and database queries:

| Model | Old Name | Correct Name | Status |
|-------|----------|--------------|--------|
| Clip | CreatedAt | CapturedAt | ✅ Fixed |
| Clip | Content | TextContent | ✅ Fixed |
| Clip | - | IsPinned | ⚠️ Missing - using Label workaround |
| SearchQuery | Query | QueryText | ✅ Fixed |
| SearchQuery | ExecutedAt | LastExecutedAt | ✅ Fixed |
| ApplicationFilter | ApplicationName | ProcessName | ✅ Fixed |

## Database Location

- **Path:** `%LocalAppData%\ClipMate\clipmate.db`
- **Created:** Automatically on first run via `Database.Migrate()`
- **Schema:** 7 tables with proper indexes and foreign keys

## Tables Created

1. **Clips** - Primary clipboard history
   - Indexed on: ContentHash, CollectionId, FolderId, CapturedAt, ClipType
   
2. **Collections** - Organizational containers
   - Indexed on: IsActive, SortOrder
   
3. **Folders** - Hierarchical organization
   - Indexed on: CollectionId, ParentFolderId
   
4. **Templates** - Reusable text templates
   - Indexed on: CollectionId, SortOrder
   
5. **SearchQueries** - Saved searches
   - Indexed on: LastExecutedAt
   
6. **ApplicationFilters** - Application-specific rules
   - Indexed on: ProcessName
   
7. **SoundEvents** - Audio feedback configuration
   - Indexed on: EventType

## Repository Implementation Notes

### Return Types
All methods return `IReadOnlyList<T>` instead of `IEnumerable<T>` for:
- Clear semantics (caller knows it's materialized)
- No accidental re-enumeration
- Better performance characteristics

### Search Implementation
Using `EF.Functions.Like()` for case-insensitive LIKE queries:
```csharp
.Where(p => EF.Functions.Like(p.TextContent ?? "", $"%{searchText}%"))
```

### Create vs Add Methods
`ClipRepository` has both:
- `CreateAsync()` - Interface requirement (returns Clip)
- `AddAsync()` - Backward compatibility (delegates to CreateAsync)

### Update Methods
All repositories return `Task<bool>`:
```csharp
public async Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
{
    _context.Set<T>().Update(entity);
    await _context.SaveChangesAsync(cancellationToken);
    return true;
}
```

## Migration Process

1. ✅ Updated Directory.Packages.props
2. ✅ Created ClipMateDbContext with entity configurations
3. ✅ Implemented all 7 EF Core repositories
4. ✅ Updated ServiceCollectionExtensions for EF Core DI
5. ✅ Created design-time factory
6. ✅ Generated initial migration
7. ✅ Verified zero compilation errors
8. ✅ Deleted old LiteDB folder

## Testing Status

- **Build:** ✅ Passing (zero errors)
- **EF Tools Version:** ⚠️ Warning about 8.0.4 vs 9.0.0 runtime (non-blocking)
- **Unit Tests:** Pending update for EF Core
- **Integration Tests:** Pending creation

## Next Steps

1. Update App.xaml.cs if needed (currently using `InitializeDatabase()` which works)
2. Test database creation on first run
3. Update unit tests for repository implementations
4. Add integration tests for database operations
5. Consider migrating Win32 P/Invoke to CsWin32 (separate task)
6. Research EventAggregator alternatives (MediatR has license issues)

## Known Issues

- ⚠️ `Clip.IsPinned` property missing from model - temporarily using `Label` field for pinned clips
- ⚠️ EF tools version 8.0.4 vs runtime 9.0.0 (non-critical, can update later)

## Performance Considerations

- DbContext registered as **Scoped** lifetime
- Repositories registered as **Scoped** lifetime
- One DbContext instance per scope/request
- Change tracking enabled by default (can optimize later if needed)
- Async/await throughout for non-blocking I/O

## Documentation Updates

- ✅ Created `architecture-decisions.md` with ADR-001 (EF Core decision)
- ✅ Updated `tasks.md` to mark Phase 2 tasks complete
- ✅ Updated `README.md` technology stack section
- ✅ Created this migration notes document

## References

- See `specs/001-clipboard-manager/architecture-decisions.md` for full rationale
- See `specs/001-clipboard-manager/tasks.md` for updated task list
- See `.github/copilot-instructions.md` for Implementation Policy
