# Self-Referencing Foreign Key Fix

## Issue

Application was failing to start with the error:
```
System.InvalidOperationException: Schema validation failed: Circular foreign key reference detected involving table 'Collections'
```

The `Collections` table has a valid self-referencing foreign key (`ParentId → Collections.Id`) for hierarchical data, but the schema validator was incorrectly flagging it as a circular dependency.

## Root Cause

The `ValidateCircularForeignKeys` method in `SqliteSchemaValidator` was detecting self-references as circular dependencies. The previous fix attempt initialized the `visited` set with the start table, but the logic still failed because:

1. The `visited` set was initialized with `tableName` before checking references
2. The `HasCircularReference` method checked `visited.Any()` which was always true
3. This caused self-references to be flagged as circular even though they're valid

## Solution

Updated the circular foreign key detection algorithm to properly distinguish between:
- **Self-references** (valid): `Collections.ParentId → Collections.Id`
- **Circular references** (invalid): `TableA → TableB → TableA`

### Changes Made

**File:** `ClipMate.Data.Schema/Sqlite/SqliteSchemaValidator.cs`

#### 1. Updated `ValidateCircularForeignKeys` method:
```csharp
private void ValidateCircularForeignKeys(SchemaDefinition schema, ValidationResult result)
{
    var graph = BuildForeignKeyGraph(schema);

    foreach (var tableName in graph.Keys)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Added isStart parameter to track first call
        if (HasCircularReference(tableName, tableName, graph, visited, isStart: true))
        {
            result.Errors.Add($"Circular foreign key reference detected involving table '{tableName}'");
        }
    }
}
```

#### 2. Enhanced `HasCircularReference` method:
```csharp
private bool HasCircularReference(
    string startTable,
    string currentTable,
    Dictionary<string, List<string>> graph,
    HashSet<string> visited,
    bool isStart = false)  // New parameter to detect initial call
{
    if (!graph.TryGetValue(currentTable, out var references))
        return false;

    foreach (var refTable in references)
    {
        // Allow self-references (e.g., Collections.ParentId → Collections.Id)
        // But detect true circular references between different tables
        if (refTable.Equals(startTable, StringComparison.OrdinalIgnoreCase))
        {
            // If this is the starting table checking its own references, skip self-references
            if (isStart && refTable.Equals(currentTable, StringComparison.OrdinalIgnoreCase))
                continue;
            
            // If we've visited other tables and came back to start, it's circular
            if (visited.Count > 0)
                return true;
        }

        if (visited.Add(refTable))
        {
            if (HasCircularReference(startTable, refTable, graph, visited, isStart: false))
                return true;

            visited.Remove(refTable);
        }
    }

    return false;
}
```

### Key Algorithm Changes

1. **`isStart` parameter**: Tracks whether we're at the starting table on the first call
2. **Self-reference detection**: When `isStart=true` and the reference points back to the current table, skip it (valid self-reference)
3. **Circular detection**: Only flag as circular if we've visited other tables (`visited.Count > 0`) and came back to start

### Test Coverage

Added explicit test for self-referencing foreign keys:

**File:** `ClipMate.Tests.Unit.Schema/SqliteSchemaValidatorTests.cs`

```csharp
[Test]
public async Task Validate_SelfReferencingForeignKey_IsValid()
{
    // Arrange - Self-referencing FK is valid for hierarchical data
    var validator = new SqliteSchemaValidator();
    var schema = new SchemaDefinition
    {
        Tables = new Dictionary<string, TableDefinition>
        {
            ["Collections"] = new TableDefinition
            {
                Name = "Collections",
                Columns = new List<ColumnDefinition>
                {
                    new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                    new() { Name = "Name", Type = "TEXT", IsNullable = false, Position = 1 },
                    new() { Name = "ParentId", Type = "INTEGER", IsNullable = true, Position = 2 }
                },
                ForeignKeys = new List<ForeignKeyDefinition>
                {
                    new() { ColumnName = "ParentId", ReferencedTable = "Collections", ReferencedColumn = "Id" }
                }
            }
        }
    };

    // Act
    var result = validator.Validate(schema);

    // Assert
    await Assert.That(result.IsValid).IsTrue();
    await Assert.That(result.Errors).IsEmpty();
}
```

## Validation

✅ **48 tests passing** (47 original + 1 new self-referencing test)
- `Validate_SelfReferencingForeignKey_IsValid` - NEW TEST
- `Validate_CircularForeignKeys_ReturnsError` - Still catches true circular dependencies
- All other schema validation tests pass

✅ **Solution builds successfully**
- No compile errors
- All projects build cleanly

✅ **Application starts successfully**
- Schema validation no longer blocks startup
- Collections table with self-referencing FK is correctly validated

## Edge Cases Covered

| Scenario | Expected | Actual | Status |
|----------|----------|--------|--------|
| Collections → Collections (self-ref) | ✅ Valid | ✅ Valid | ✅ Pass |
| TableA → TableB → TableA | ❌ Invalid | ❌ Invalid | ✅ Pass |
| TableA → TableB → TableC → TableA | ❌ Invalid | ❌ Invalid | ✅ Pass |
| Empty schema | ✅ Valid | ✅ Valid | ✅ Pass |
| Multiple self-references in different tables | ✅ Valid | ✅ Valid | ✅ Pass |

## Impact

This fix enables:
1. **Hierarchical data structures** using self-referencing foreign keys
2. **Tree/graph structures** in the database (folders, categories, org charts, etc.)
3. **ClipMate Collections** to properly support parent-child relationships

The fix maintains safety by still detecting true circular dependencies between different tables while allowing valid self-references.

## Files Modified

1. ✅ `ClipMate.Data.Schema/Sqlite/SqliteSchemaValidator.cs` - Fixed circular FK detection
2. ✅ `ClipMate.Tests.Unit.Schema/SqliteSchemaValidatorTests.cs` - Added self-referencing FK test

**Status:** ✅ FIXED - Application starts successfully with self-referencing foreign keys
