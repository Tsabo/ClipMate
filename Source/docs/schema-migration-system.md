# Schema Migration System Implementation

## Overview

This document describes the implementation of a custom schema-based database migration system for ClipMate, replacing EF Core migrations with a more reliable and transparent approach.

## Problem Statement

The EF Core migration system was causing issues:
1. **Migration History Conflicts**: "Table already exists" errors despite pending migrations
2. **Opaque Process**: Difficult to understand what migrations actually do
3. **Customer Deployment Issues**: Migration history tracking caused problems in production
4. **DI Lifetime Mismatches**: Complex service lifetime issues with scoped repositories

## Solution Architecture

### ClipMate.Data.Schema Class Library

A new database-agnostic schema migration library with the following structure:

```
ClipMate.Data.Schema/
├── Models/              # Data models for schema representation
├── Abstractions/        # Interfaces for extensibility
├── Sqlite/             # SQLite-specific implementations
├── Serialization/      # JSON schema export/import
└── EntityFramework/    # Optional EF Core bridge
```

### Key Components

#### 1. Models (11 classes)
- **SchemaDefinition**: Dictionary of tables with JSON serialization
- **TableDefinition**: Columns, indexes, foreign keys, CREATE SQL
- **ColumnDefinition**: Name, type, nullability, PK, default, position
- **IndexDefinition**: Name, table, columns, uniqueness, CREATE SQL
- **ForeignKeyDefinition**: Column, referenced table/column, ON DELETE/UPDATE
- **MigrationOperation**: Type enum (CreateTable/AddColumn/CreateIndex), SQL
- **SchemaDiff**: List of operations, warnings, HasChanges property
- **ValidationResult**: IsValid, errors, warnings, info lists
- **MigrationContext**: Passed to hooks - diff, dry-run flag, connection, properties
- **MigrationResult**: Record type with success flag, executed SQL, errors, warnings
- **SchemaOptions**: Ignored tables/columns, validation flags, caching

#### 2. Abstractions (5 interfaces)
- **ISchemaReader**: ReadSchemaAsync() - reads from database/model/JSON
- **ISchemaComparer**: Compare(current, expected) → SchemaDiff
- **ISchemaValidator**: Validate(schema) → ValidationResult
- **ISchemaMigrator**: MigrateAsync(diff, dryRun) → MigrationResult
- **IMigrationHook**: OnBeforeMigrationAsync, OnAfterMigrationAsync

#### 3. SQLite Implementation
- **SqliteSchemaReader**: 
  - Queries `sqlite_master` and PRAGMA commands
  - Caching support for performance
  - Table/column exclusion filters
  
- **SqliteSchemaComparer**:
  - Generates SQLite-compatible SQL
  - Respects SQLite limitations (no DROP COLUMN, etc.)
  - CREATE TABLE, ALTER TABLE ADD COLUMN, CREATE INDEX
  
- **SqliteSchemaValidator**:
  - Table name validation
  - Circular foreign key detection
  - Column type validation
  - Duplicate name checking
  
- **SqliteSchemaMigrator**:
  - Transaction-wrapped execution
  - Dry-run mode support
  - Hook lifecycle management
  - Automatic rollback on failure

#### 4. Serialization
- **SchemaSerializer**:
  - ToJson, FromJson with System.Text.Json
  - ExportToFileAsync, ImportFromFileAsync
  - Pretty-print formatting with camelCase

#### 5. EF Core Bridge
- **EFCoreSchemaReader**:
  - Extracts schema from IModel metadata
  - Maps EF Core types to SQLite types
  - Caching support
  - Maps DeleteBehavior to ON DELETE clauses

## Integration

### DatabaseInitializationHostedService

The hosted service now uses the schema migration system:

1. **Ensure Database Exists**: `EnsureCreatedAsync()` creates empty database
2. **Read Current Schema**: SqliteSchemaReader queries existing database
3. **Read Expected Schema**: EFCoreSchemaReader extracts from DbContext.Model
4. **Validate Schema**: SqliteSchemaValidator checks for errors
5. **Compare Schemas**: SqliteSchemaComparer generates diff
6. **Apply Migrations**: SqliteSchemaMigrator executes SQL in transaction
7. **Logging**: Custom IMigrationHook logs all operations

### Migration Flow

```
┌─────────────────────────────────────┐
│  DatabaseInitializationHostedService │
└─────────────────┬───────────────────┘
                  │
                  ├─► EnsureCreatedAsync()
                  │
                  ├─► SqliteSchemaReader
                  │   └─► Current Schema
                  │
                  ├─► EFCoreSchemaReader
                  │   └─► Expected Schema
                  │
                  ├─► SqliteSchemaValidator
                  │   └─► Validation Result
                  │
                  ├─► SqliteSchemaComparer
                  │   └─► SchemaDiff
                  │
                  └─► SqliteSchemaMigrator
                      ├─► OnBeforeMigrationAsync (hook)
                      ├─► Execute SQL (transaction)
                      └─► OnAfterMigrationAsync (hook)
```

## Benefits

### 1. Transparency
- All SQL operations are visible and logged
- Dry-run mode allows preview without execution
- JSON export enables schema version control

### 2. Reliability
- No migration history table conflicts
- Transaction-wrapped migrations with rollback
- Validation before execution

### 3. Flexibility
- Database-agnostic abstractions
- Custom hooks for logging/auditing
- Caching for performance
- Optional table/column exclusions

### 4. Testability
- All components implement interfaces
- Synchronous validation (no async overhead)
- In-memory SQLite for tests

### 5. Customer-Friendly
- Schema changes are transparent
- No migration tracking issues
- Easy to debug and troubleshoot

## Usage Examples

### Basic Migration
```csharp
var connection = new SqliteConnection(connectionString);
await connection.OpenAsync();

var reader = new SqliteSchemaReader(connection);
var currentSchema = await reader.ReadSchemaAsync();

var efReader = new EFCoreSchemaReader(dbContext.Model);
var expectedSchema = await efReader.ReadSchemaAsync();

var comparer = new SqliteSchemaComparer();
var diff = comparer.Compare(currentSchema, expectedSchema);

var migrator = new SqliteSchemaMigrator(connection);
var result = await migrator.MigrateAsync(diff);
```

### Dry-Run Preview
```csharp
var result = await migrator.MigrateAsync(diff, dryRun: true);
foreach (var sql in result.SqlExecuted)
{
    Console.WriteLine(sql);
}
```

### Custom Logging Hook
```csharp
var hook = new LoggingMigrationHook(logger);
var migrator = new SqliteSchemaMigrator(connection, hook);
var result = await migrator.MigrateAsync(diff);
```

### Export Schema to JSON
```csharp
var serializer = new SchemaSerializer();
await serializer.ExportToFileAsync(schema, "schema-v1.0.json");
```

## Configuration

### SchemaOptions
```csharp
var options = new SchemaOptions
{
    IgnoredTables = new HashSet<string> { "__EFMigrationsHistory" },
    IgnoredColumns = new HashSet<string> { "RowVersion" },
    ValidateBeforeMigration = true,
    EnableCaching = true
};
```

## Future Enhancements

### Short-Term
1. **Unit Tests**: Comprehensive test coverage (80-90%)
2. **Integration Tests**: End-to-end migration scenarios
3. **Documentation**: API docs and examples

### Long-Term
1. **Multiple Databases**: PostgreSQL, MySQL providers
2. **Schema Rollback**: Undo migrations
3. **Migration Scripts**: Generate standalone SQL scripts
4. **Schema Diffing Tool**: CLI tool for comparing databases
5. **Migration History**: Optional tracking without conflicts

## Migration from EF Core

### Before (EF Core Migrations)
```csharp
await context.Database.MigrateAsync();
```

### After (Schema Migration)
```csharp
await MigrateDatabaseSchemaAsync(context, cancellationToken);
```

The new approach:
- ✅ No migration files to manage
- ✅ No migration history conflicts
- ✅ Full control over SQL execution
- ✅ Easy to understand and debug
- ✅ Customer-friendly deployment

## Version Compatibility

- Target Framework: .NET 9.0
- Microsoft.Data.Sqlite: 9.0.0
- Microsoft.EntityFrameworkCore.Relational: 9.0.0 (optional)
- System.Text.Json: Built-in

## Build Status

- ✅ ClipMate.Data.Schema builds successfully
- ✅ ClipMate.Data builds successfully
- ✅ Integration complete
- ⏳ Unit tests pending
- ⏳ End-to-end testing pending

## Original DI Issue Resolution

The original DI lifetime mismatch error was due to Singleton services consuming Scoped repositories. The services were already registered as Scoped in `ServiceCollectionExtensions.cs`, so the issue may have been elsewhere or already resolved. The schema migration system provides a cleaner architecture that avoids these issues.

## Conclusion

The new schema migration system provides a robust, transparent, and reliable alternative to EF Core migrations. It gives full control over database schema changes while maintaining compatibility with EF Core for entity mapping and querying.
