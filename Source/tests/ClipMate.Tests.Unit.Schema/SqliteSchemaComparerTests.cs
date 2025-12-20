using ClipMate.Data.Schema.Models;
using ClipMate.Data.Schema.Sqlite;

namespace ClipMate.Tests.Unit.Schema;

public class SqliteSchemaComparerTests
{
    [Test]
    public async Task Compare_EmptySchemas_ReturnsNoDifferences()
    {
        // Arrange
        var comparer = new SqliteSchemaComparer();
        var current = new SchemaDefinition();
        var expected = new SchemaDefinition();

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff).IsNotNull();
        await Assert.That(diff.HasChanges).IsFalse();
        await Assert.That(diff.Operations).IsEmpty();
    }

    [Test]
    public async Task Compare_NewTable_GeneratesCreateTableOperation()
    {
        // Arrange
        var comparer = new SqliteSchemaComparer();
        var current = new SchemaDefinition();
        var expected = new SchemaDefinition
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
                    CreateSql = "CREATE TABLE Users (...)",
                },
            },
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.Operations).Count().IsEqualTo(1);
        await Assert.That(diff.Operations[0].Type).IsEqualTo(MigrationOperationType.CreateTable);
        await Assert.That(diff.Operations[0].TableName).IsEqualTo("Users");
    }

    [Test]
    public async Task Compare_NewColumn_GeneratesAddColumnOperation()
    {
        // Arrange
        var comparer = new SqliteSchemaComparer();
        var current = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns = [new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }],
                },
            },
        };

        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Email", Type = "TEXT", IsNullable = true, Position = 1 },
                    ],
                },
            },
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.Operations).Count().IsEqualTo(1);
        await Assert.That(diff.Operations[0].Type).IsEqualTo(MigrationOperationType.AddColumn);
        await Assert.That(diff.Operations[0].TableName).IsEqualTo("Users");
        await Assert.That(diff.Operations[0].ColumnName).IsEqualTo("Email");
    }

    [Test]
    public async Task Compare_NewIndex_GeneratesCreateIndexOperation()
    {
        // Arrange
        var comparer = new SqliteSchemaComparer();
        var current = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Email", Type = "TEXT", IsNullable = true, Position = 1 },
                    ],
                    Indexes = [],
                },
            },
        };

        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Email", Type = "TEXT", IsNullable = true, Position = 1 },
                    ],
                    Indexes =
                    [
                        new IndexDefinition
                        {
                            Name = "IX_Users_Email",
                            TableName = "Users",
                            Columns = ["Email"],
                            IsUnique = false,
                            CreateSql = "CREATE INDEX IX_Users_Email ON Users (Email)",
                        },
                    ],
                },
            },
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.Operations).Count().IsEqualTo(1);
        await Assert.That(diff.Operations[0].Type).IsEqualTo(MigrationOperationType.CreateIndex);
        await Assert.That(diff.Operations[0].IndexName).IsEqualTo("IX_Users_Email");
    }

    [Test]
    public async Task Compare_RemovedTable_GeneratesWarning()
    {
        // Arrange
        var comparer = new SqliteSchemaComparer();
        var current = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns = [new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }],
                },
            },
        };

        var expected = new SchemaDefinition();

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.Warnings).Count().IsGreaterThan(0);
        await Assert.That(diff.Warnings.Any(p => p.Contains("Users"))).IsTrue();
    }

    [Test]
    public async Task Compare_RemovedColumn_GeneratesWarning()
    {
        // Arrange
        var comparer = new SqliteSchemaComparer();
        var current = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "OldColumn", Type = "TEXT", IsNullable = true, Position = 1 },
                    ],
                },
            },
        };

        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns = [new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }],
                },
            },
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.Warnings).Count().IsGreaterThan(0);
        await Assert.That(diff.Warnings.Any(p => p.Contains("OldColumn"))).IsTrue();
    }

    [Test]
    public async Task Compare_ColumnTypeChange_GeneratesWarning()
    {
        // Arrange
        var comparer = new SqliteSchemaComparer();
        var current = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Age", Type = "TEXT", IsNullable = true, Position = 1 },
                    ],
                },
            },
        };

        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Age", Type = "INTEGER", IsNullable = true, Position = 1 },
                    ],
                },
            },
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.Warnings).Count().IsGreaterThan(0);
        await Assert.That(diff.Warnings.Any(p => p.Contains("Age") && p.Contains("type"))).IsTrue();
    }

    [Test]
    public async Task Compare_MultipleChanges_GeneratesAllOperations()
    {
        // Arrange
        var comparer = new SqliteSchemaComparer();
        var current = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns = [new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }],
                },
            },
        };

        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new()
                {
                    Name = "Users",
                    Columns =
                    [
                        new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new ColumnDefinition { Name = "Email", Type = "TEXT", IsNullable = true, Position = 1 },
                    ],
                },
                ["Products"] = new()
                {
                    Name = "Products",
                    Columns = [new ColumnDefinition { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }],
                    CreateSql = "CREATE TABLE Products (...)",
                },
            },
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.Operations).Count().IsEqualTo(2);
        await Assert.That(diff.Operations.Any(p => p is { Type: MigrationOperationType.CreateTable, TableName: "Products" })).IsTrue();
        await Assert.That(diff.Operations.Any(p => p is { Type: MigrationOperationType.AddColumn, ColumnName: "Email" })).IsTrue();
    }
}
