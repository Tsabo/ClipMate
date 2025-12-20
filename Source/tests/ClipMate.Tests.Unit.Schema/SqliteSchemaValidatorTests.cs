using ClipMate.Data.Schema.Models;
using ClipMate.Data.Schema.Sqlite;

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
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Name", Type = "TEXT", IsNullable = false, Position = 1 },
                    ],
                },
            },
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
                ["123Invalid"] = new()
                {
                    Name = "123Invalid",
                    Columns = [new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }],
                },
            },
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors).Count().IsGreaterThan(0);
        await Assert.That(result.Errors.Any(p => p.Contains("123Invalid") && p.Contains("table name"))).IsTrue();
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
                ["sqlite_master"] = new()
                {
                    Name = "sqlite_master",
                    Columns = [new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }],
                },
            },
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(p => p.Contains("sqlite_") && p.Contains("reserved"))).IsTrue();
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
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns = [new ColumnDefinition { Name = "123Invalid", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }],
                },
            },
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(p => p.Contains("123Invalid") && p.Contains("column name"))).IsTrue();
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
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns = [new ColumnDefinition { Name = "Id", Type = "INVALID_TYPE", IsPrimaryKey = true, IsNullable = false, Position = 0 }],
                },
            },
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(p => p.Contains("INVALID_TYPE") && p.Contains("type"))).IsTrue();
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
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Id", Type = "TEXT", IsNullable = false, Position = 1 },
                    ],
                },
            },
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(p => p.Contains("duplicate") && p.Contains("Id"))).IsTrue();
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
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Email", Type = "TEXT", IsNullable = false, Position = 1 },
                    ],
                    Indexes =
                    [
                        new IndexDefinition { Name = "IX_Users", TableName = "Users", Columns = ["Id"], IsUnique = false },
                        new IndexDefinition { Name = "IX_Users", TableName = "Users", Columns = ["Email"], IsUnique = false },
                    ],
                },
            },
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(p => p.Contains("duplicate") && p.Contains("IX_Users"))).IsTrue();
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
                ["Collections"] = new()
                {
                    Name = "Collections",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Name", Type = "TEXT", IsNullable = false, Position = 1 },
                        new ColumnDefinition { Name = "ParentId", Type = "INTEGER", IsNullable = true, Position = 2 },
                    ],
                    ForeignKeys = [new ForeignKeyDefinition { ColumnName = "ParentId", ReferencedTable = "Collections", ReferencedColumn = "Id" }],
                },
            },
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
                ["TableA"] = new()
                {
                    Name = "TableA",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "TableBId", Type = "INTEGER", IsNullable = true, Position = 1 },
                    ],
                    ForeignKeys = [new ForeignKeyDefinition { ColumnName = "TableBId", ReferencedTable = "TableB", ReferencedColumn = "Id" }],
                },
                ["TableB"] = new()
                {
                    Name = "TableB",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "TableAId", Type = "INTEGER", IsNullable = true, Position = 1 },
                    ],
                    ForeignKeys = [new ForeignKeyDefinition { ColumnName = "TableAId", ReferencedTable = "TableA", ReferencedColumn = "Id" }],
                },
            },
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(p => p.Contains("circular") && p.Contains("foreign key"))).IsTrue();
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
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "DepartmentId", Type = "INTEGER", IsNullable = true, Position = 1 },
                    ],
                    ForeignKeys = [new ForeignKeyDefinition { ColumnName = "DepartmentId", ReferencedTable = "NonExistentTable", ReferencedColumn = "Id" }],
                },
            },
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(p => p.Contains("NonExistentTable") && p.Contains("does not exist"))).IsTrue();
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
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns = [new ColumnDefinition { Name = "Name", Type = "TEXT", IsPrimaryKey = false, IsNullable = false, Position = 0 }],
                },
            },
        };

        // Act
        var result = validator.Validate(schema);

        // Assert
        await Assert.That(result.Warnings).Count().IsGreaterThan(0);
        await Assert.That(result.Warnings.Any(p => p.Contains("Users") && p.Contains("primary key"))).IsTrue();
    }
}
