using ClipMate.Data.Schema.Models;
using ClipMate.Data.Schema.Sqlite;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

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
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Name", Type = "TEXT", IsNullable = false, Position = 1 }
                    },
                    CreateSql = "CREATE TABLE Users (...)"
                }
            }
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.Operations).HasCount().EqualTo(1);
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
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }
                    }
                }
            }
        };
        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Email", Type = "TEXT", IsNullable = true, Position = 1 }
                    }
                }
            }
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.Operations).HasCount().EqualTo(1);
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
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Email", Type = "TEXT", IsNullable = true, Position = 1 }
                    },
                    Indexes = new List<IndexDefinition>()
                }
            }
        };
        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Email", Type = "TEXT", IsNullable = true, Position = 1 }
                    },
                    Indexes = new List<IndexDefinition>
                    {
                        new()
                        {
                            Name = "IX_Users_Email",
                            TableName = "Users",
                            Columns = new List<string> { "Email" },
                            IsUnique = false,
                            CreateSql = "CREATE INDEX IX_Users_Email ON Users (Email)"
                        }
                    }
                }
            }
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.Operations).HasCount().EqualTo(1);
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
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }
                    }
                }
            }
        };
        var expected = new SchemaDefinition();

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.Warnings).HasCount().GreaterThan(0);
        await Assert.That(diff.Warnings.Any(w => w.Contains("Users"))).IsTrue();
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
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "OldColumn", Type = "TEXT", IsNullable = true, Position = 1 }
                    }
                }
            }
        };
        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }
                    }
                }
            }
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.Warnings).HasCount().GreaterThan(0);
        await Assert.That(diff.Warnings.Any(w => w.Contains("OldColumn"))).IsTrue();
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
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Age", Type = "TEXT", IsNullable = true, Position = 1 }
                    }
                }
            }
        };
        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Age", Type = "INTEGER", IsNullable = true, Position = 1 }
                    }
                }
            }
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.Warnings).HasCount().GreaterThan(0);
        await Assert.That(diff.Warnings.Any(w => w.Contains("Age") && w.Contains("type"))).IsTrue();
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
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }
                    }
                }
            }
        };
        var expected = new SchemaDefinition
        {
            Tables = new Dictionary<string, TableDefinition>
            {
                ["Users"] = new TableDefinition
                {
                    Name = "Users",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 },
                        new() { Name = "Email", Type = "TEXT", IsNullable = true, Position = 1 }
                    }
                },
                ["Products"] = new TableDefinition
                {
                    Name = "Products",
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Id", Type = "INTEGER", IsPrimaryKey = true, IsNullable = false, Position = 0 }
                    },
                    CreateSql = "CREATE TABLE Products (...)"
                }
            }
        };

        // Act
        var diff = comparer.Compare(current, expected);

        // Assert
        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.Operations).HasCount().EqualTo(2);
        await Assert.That(diff.Operations.Any(op => op.Type == MigrationOperationType.CreateTable && op.TableName == "Products")).IsTrue();
        await Assert.That(diff.Operations.Any(op => op.Type == MigrationOperationType.AddColumn && op.ColumnName == "Email")).IsTrue();
    }
}
