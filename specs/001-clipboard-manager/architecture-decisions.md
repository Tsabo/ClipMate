# Architecture Decision Records

## ADR-001: Database Layer - Entity Framework Core + SQLite

**Date:** 2025-11-12  
**Status:** Accepted  
**Decision Makers:** Development Team

### Context

During Phase 2 implementation, we initially implemented the data layer using LiteDB 5.0.21. However, upon review against our Implementation Policy (research Microsoft solutions first, third-party requires approval, custom as last resort), we identified that:

1. LiteDB is a third-party NoSQL database with no abstraction layer
2. The team has "years of experience with Entity Framework"
3. Microsoft provides robust data access solutions through Entity Framework Core
4. Using EF Core would provide better testability, migration support, and abstraction

### Decision

**Replace LiteDB with Entity Framework Core 9.0.0 + SQLite provider**

**Rationale:**
- **Microsoft Official:** EF Core is the official Microsoft ORM, aligning with our policy to prefer Microsoft solutions
- **Team Expertise:** Leverages existing team experience with Entity Framework
- **Abstraction Layer:** Provides proper repository pattern implementation with testability
- **Migration Support:** Built-in schema migration and versioning capabilities
- **SQLite Choice:** Lightweight, serverless, cross-platform, perfect for desktop applications
- **No Data Migration:** This is a new application with no existing data to migrate

### Implementation Details

**Packages Used:**
- `Microsoft.EntityFrameworkCore.Sqlite` 9.0.0
- `Microsoft.EntityFrameworkCore.Design` 9.0.0 (design-time only)
- `Microsoft.EntityFrameworkCore.Tools` 9.0.0 (tooling)

**Key Components Created:**

1. **ClipMateDbContext** - EF Core DbContext with 7 entity configurations:
   - Clips (primary clipboard history)
   - Collections (organizational containers)
   - Folders (hierarchical organization)
   - Templates (reusable text templates)
   - SearchQueries (saved searches)
   - ApplicationFilters (application-specific rules)
   - SoundEvents (audio feedback configuration)

2. **Repository Implementations** - All 7 repositories using async/await patterns:
   - Proper interface implementation with `Task<IReadOnlyList<T>>`
   - EF.Functions.Like for case-insensitive search
   - Scoped lifetime registration for proper DbContext management

3. **Design-Time Factory** - `ClipMateDbContextFactory` for migrations tooling

4. **DI Registration** - `ServiceCollectionExtensions.AddClipMateData()`:
   - Registers DbContext with SQLite connection string
   - Registers all repositories as scoped services
   - `InitializeDatabase()` applies pending migrations via `Database.Migrate()`

**Property Name Corrections:**
During migration, we aligned property names between Core models and Data layer queries:
- Clip: `CapturedAt` (not `CreatedAt`), `TextContent` (not `Content`)
- SearchQuery: `QueryText` (not `Query`), `LastExecutedAt` (not `ExecutedAt`)
- ApplicationFilter: `ProcessName` (not `ApplicationName`)
- Workaround: Clip.`IsPinned` property missing, temporarily using `Label` field

**Database Location:**
- Path: `%LocalAppData%\ClipMate\clipmate.db`
- Automatically created on first run via migrations

### Consequences

**Positive:**
- ✅ Full LINQ query support with compile-time checking
- ✅ Automatic schema migrations and versioning
- ✅ Better testability (can use InMemory provider for tests)
- ✅ Leverages team's existing EF experience
- ✅ Follows Implementation Policy (Microsoft-first approach)
- ✅ Strong typing and navigation properties
- ✅ Built-in change tracking

**Negative:**
- ⚠️ Slightly larger dependency footprint than LiteDB
- ⚠️ Need EF tools version update (currently 8.0.4 vs runtime 9.0.0)

**Neutral:**
- Initial migration created: `20251112151350_InitialCreate.cs`
- All 7 tables with proper indexes and foreign keys
- Zero compilation errors after migration

### Alternatives Considered

1. **LiteDB** (initially implemented)
   - Pros: Lightweight, simple, NoSQL flexibility
   - Cons: No abstraction layer, unfamiliar to team, limited query capabilities
   - Rejected: Doesn't align with team expertise or Implementation Policy

2. **Dapper + SQLite**
   - Pros: Lightweight micro-ORM, fast performance
   - Cons: Manual SQL, no migrations, more boilerplate
   - Rejected: Team prefers EF's conventions and migration support

3. **System.Data.SQLite (ADO.NET)**
   - Pros: Maximum performance, full control
   - Cons: Excessive boilerplate, manual schema management
   - Rejected: Reinventing what EF Core provides

### Related Decisions

- See ADR-002 for Win32 Platform Layer decisions
- See ADR-003 for MVVM/DI decisions
- See copilot-instructions.md Implementation Policy

---

## ADR-002: MVVM Implementation - CommunityToolkit.Mvvm

**Date:** 2025-11-12  
**Status:** Accepted  
**Decision Makers:** Development Team

### Context

WPF application requires MVVM pattern implementation for separation of concerns and testability.

### Decision

**Use CommunityToolkit.Mvvm 8.3.2** (formerly MVVM Toolkit)

**Rationale:**
- **Microsoft Official:** Part of .NET Community Toolkit, maintained by Microsoft
- **Modern C# Features:** Uses source generators for performance
- **Minimal Boilerplate:** Attributes-based approach reduces code
- **Well Documented:** Extensive Microsoft documentation and samples
- **Approved:** Meets Implementation Policy criteria (Microsoft solution)

**Implementation:**
- `ViewModelBase` inherits from `ObservableObject`
- Using `[ObservableProperty]` attribute for property generation
- Using `[RelayCommand]` for command generation
- Registered in DI container with appropriate lifetime

### Consequences

**Positive:**
- ✅ Reduces boilerplate significantly
- ✅ Source generators = better performance
- ✅ Microsoft official support
- ✅ Modern C# 11+ features

**Negative:**
- None identified

---

## ADR-003: Dependency Injection - Microsoft.Extensions.DependencyInjection

**Date:** 2025-11-12  
**Status:** Accepted  
**Decision Makers:** Development Team

### Context

Desktop application needs service registration and dependency injection.

### Decision

**Use Microsoft.Extensions.DependencyInjection 9.0.0**

**Rationale:**
- **Microsoft Official:** Standard .NET DI container
- **Consistent:** Same DI used across ASP.NET Core, MAUI, etc.
- **Familiar:** Team knows this from other .NET projects
- **Approved:** Meets Implementation Policy criteria

**Implementation:**
- App.xaml.cs builds ServiceProvider on startup
- Extension methods in each layer: `AddClipMateCore()`, `AddClipMateData()`
- Repositories registered as Scoped
- Services registered as Transient or Singleton based on state
- ViewModels registered as Transient

### Consequences

**Positive:**
- ✅ Standard .NET approach
- ✅ Easy to test with mock services
- ✅ Clear lifetime management

---

## ADR-004: Logging - Microsoft.Extensions.Logging

**Date:** 2025-11-12  
**Status:** Accepted  
**Decision Makers:** Development Team

### Context

Initially implemented custom `ILogger` interface and `FileLoggerProvider`. User questioned: "Not sure why we are recreating a logger interface when .NET has one out of the box."

### Decision

**Use Microsoft.Extensions.Logging 9.0.0** with Console and Debug providers

**Rationale:**
- **Microsoft Official:** Standard .NET logging abstraction
- **Don't Reinvent:** Violates DRY principle to create custom logger
- **Rich Ecosystem:** Can add Serilog, NLog, Application Insights later
- **Approved:** Meets Implementation Policy criteria

**Implementation:**
- Removed custom `Logging/` folder entirely
- Using `ILogger<T>` generic interface
- Console provider for debug builds
- Debug provider for Visual Studio output
- Future: Can add file logging via Serilog if needed

### Consequences

**Positive:**
- ✅ Standard .NET logging
- ✅ Can swap providers without code changes
- ✅ Removed unnecessary custom code

**Negative:**
- None identified

---

## ADR-005: Platform Layer - PENDING Win32 P/Invoke Migration

**Date:** 2025-11-12  
**Status:** Proposed  
**Decision Makers:** Development Team

### Context

Currently using manual P/Invoke declarations in `Win32Methods` and `Win32Constants` classes. Microsoft provides `Microsoft.Windows.CsWin32` source generator for type-safe Win32 interop.

### Proposed Decision

**Migrate to Microsoft.Windows.CsWin32 source generator**

**Rationale:**
- **Microsoft Official:** Official Microsoft solution for Win32 interop
- **Type Safe:** Source-generated code with proper signatures
- **Maintained:** Updated with Windows SDK releases
- **Less Error-Prone:** No manual P/Invoke declarations
- **Approved by User:** User confirmed this approach

**Implementation Plan:**
1. Add `Microsoft.Windows.CsWin32` NuGet package
2. Create `NativeMethods.txt` with required API names
3. Remove manual `Win32Methods.cs` and `Win32Constants.cs`
4. Update `ClipboardMonitor`, `HotkeyManager`, `DpiHelper` to use generated code

**Status:** Pending implementation

---

## ADR-006: Event Aggregation - PENDING Research

**Date:** 2025-11-12  
**Status:** Research Required  
**Decision Makers:** Development Team

### Context

Currently using custom `EventAggregator` implementation for loosely-coupled messaging. MediatR was considered but rejected due to license change to proprietary license.

### Options to Research

1. **Keep Custom EventAggregator**
   - Pros: Simple, works, no dependencies
   - Cons: Limited features vs dedicated libraries

2. **Wolverine**
   - Pros: Modern, message-based
   - Cons: May be overkill for desktop app

3. **Mediator (MIT License)**
   - Pros: MediatR-like API, MIT license
   - Cons: Need to verify maintenance and features

4. **Simple.EventAggregator or other alternatives**
   - Need research

**Status:** Awaiting research and decision

---

## Summary of Current Architecture

### Technology Stack
- **.NET 10.0** (preview)
- **WPF** - Desktop UI framework
- **EF Core 9.0.0 + SQLite** - Data persistence
- **CommunityToolkit.Mvvm 8.3.2** - MVVM implementation
- **Microsoft.Extensions.DependencyInjection 9.0.0** - IoC container
- **Microsoft.Extensions.Logging 9.0.0** - Logging abstraction
- **Central Package Management** - Version management via Directory.Packages.props

### Project Structure
```
Source/
  ClipMate.sln
  src/
    ClipMate.Core/        - Domain models, interfaces, base classes
    ClipMate.Data/        - EF Core repositories, DbContext, migrations
    ClipMate.Platform/    - Win32 interop (clipboard, hotkeys, DPI)
    ClipMate.App/         - WPF application, DI bootstrapping
  tests/
    ClipMate.Tests.Unit/
    ClipMate.Tests.Integration/
```

### Layer Dependencies
- App → Data → Core
- App → Platform
- Platform → Core
- No circular dependencies

### Key Principles Followed
1. **Microsoft-First:** Prefer Microsoft solutions over third-party
2. **Team Expertise:** Leverage existing EF experience
3. **No Reinvention:** Don't recreate what .NET provides
4. **Approval Required:** Third-party packages need justification
5. **Documentation:** Document all major decisions (this file)
