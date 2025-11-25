using ClipMate.Data.Schema.Models;
using ClipMate.Data.Schema.Sqlite;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.Schema;

public class SqliteSchemaValidatorTests
{
    // ===== Valid Schema Tests =====
    
    [Test]
    public async Task Validate_EmptySchema_ReturnsValid()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition();

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task Validate_ValidSchema_ReturnsValid()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Name", Type = "TEXT", IsNullable = false, Position = 1 }
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

    // ===== Table Name Validation Tests =====

    [Test]
    public async Task Validate_InvalidTableName_ReturnsError()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["123Invalid"] = new TableDefinition
                {
                    Name = "123Invalid",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors).HasCount().GreaterThan(0);
        await Assert.That(result.Errors.Any(e => e.Contains("123Invalid") && e.Contains("table name"))).IsTrue();
    }

    [Test]
    public async Task Validate_ReservedTableName_ReturnsError()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["sqlite_master"] = new TableDefinition
                {
                    Name = "sqlite_master",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Contains("sqlite_") && e.Contains("reserved"))).IsTrue();
    }

    // ===== Column Validation Tests =====

    [Test]
    public async Task Validate_InvalidColumnName_ReturnsError()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "123Invalid", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Contains("123Invalid") && e.Contains("column name"))).IsTrue();
    }

    [Test]
    public async Task Validate_InvalidColumnType_ReturnsError()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INVALID_TYPE", IsPrimaryKey = true, IsNullable = false, Position = 0 }
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Contains("INVALID_TYPE") && e.Contains("type"))).IsTrue();
    }

    // ===== Duplicate Name Tests =====

    [Test]
    public async Task Validate_DuplicateColumnNames_ReturnsError()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Id", Type = "TEXT", IsNullable = false, Position = 1 }
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Contains("duplicate") && e.Contains("Id"))).IsTrue();
    }

    [Test]
    public async Task Validate_DuplicateIndexNames_ReturnsError()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Email", Type = "TEXT", IsNullable = false, Position = 1 }
                    },
                    Indexes = new List<IndexDefinition>
                    {
                        new() { Name = "IX_Users", TableName = "Users", Columns = new List<string> { "Id" }, IsUnique = false },
                        new() { Name = "IX_Users", TableName = "Users", Columns = new List<string> { "Email" }, IsUnique = false }
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Contains("duplicate") && e.Contains("IX_Users"))).IsTrue();
    }

    // ===== Foreign Key Tests =====

    [Test]
    public async Task Validate_SelfReferencingForeignKey_IsValid()
    {
        // Arrange - Self-referencing FK is valid for hierarchical data (e.g., Collections.ParentId → Collections.Id)
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

    [Test]
    public async Task Validate_CircularForeignKeys_ReturnsError()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["TableA"] = new TableDefinition
                {
                    Name = "TableA",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "TableBId", Type = "INTEGER", IsNullable = true, Position = 1 }
                    },
                    ForeignKeys = new List<ForeignKeyDefinition>
                    {
                        new() { ColumnName = "TableBId", ReferencedTable = "TableB", ReferencedColumn = "Id" }
                    }
                },
                ["TableB"] = new TableDefinition
                {
                    Name = "TableB",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "TableAId", Type = "INTEGER", IsNullable = true, Position = 1 }
                    },
                    ForeignKeys = new List<ForeignKeyDefinition>
                    {
                        new() { ColumnName = "TableAId", ReferencedTable = "TableA", ReferencedColumn = "Id" }
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Contains("circular") && e.Contains("foreign key"))).IsTrue();
    }

    [Test]
    public async Task Validate_ForeignKeyToNonExistentTable_ReturnsError()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "DepartmentId", Type = "INTEGER", IsNullable = true, Position = 1 }
                    },
                    ForeignKeys = new List<ForeignKeyDefinition>
                    {
                        new() { ColumnName = "DepartmentId", ReferencedTable = "NonExistentTable", ReferencedColumn = "Id" }
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Contains("NonExistentTable") && e.Contains("does not exist"))).IsTrue();
    }

    // ===== Primary Key Tests =====

    [Test]
    public async Task Validate_TableWithoutPrimaryKey_ReturnsWarning()
    {
        // Arrange
        var validator = new SqliteSchemaValidator();
        var schema = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Name", Type = "TEXT", IsPrimaryKey = false, IsNullable = false, Position = 0 }
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.Warnings).HasCount().GreaterThan(0);
        await Assert.That(result.Warnings.Any(w => w.Contains("Users") && w.Contains("primary key"))).IsTrue();
    }
}
