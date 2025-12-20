using ClipMate.Data.Schema.Models;
using ClipMate.Data.Schema.Sqlite;
using Microsoft.Data.Sqlite;

namespace ClipMate.Tests.Unit.Schema;

public class SqliteSchemaReaderTests
{
    [Test]
    public async Task ReadSchemaAsync_EmptyDatabase_ReturnsEmptySchema()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var reader = new SqliteSchemaReader(connection);

        // Act
        var schema = await reader.ReadSchemaAsync();

        // Assert
        await Assert.That(schema).IsNotNull();
        await Assert.That(schema.Tables).IsEmpty();
    }

    [Test]
    public async Task ReadSchemaAsync_SingleTable_ReadsTableCorrectly()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE Users (
                              Id INTEGER PRIMARY KEY AUTOINCREMENT,
                              Name TEXT NOT NULL,
                              Email TEXT,
                              Age INTEGER DEFAULT 0
                          )
                          """;

        await cmd.ExecuteNonQueryAsync();

        var reader = new SqliteSchemaReader(connection);

        // Act
        var schema = await reader.ReadSchemaAsync();

        // Assert
        await Assert.That(schema.Tables).Count().IsEqualTo(1);
        await Assert.That(schema.Tables.ContainsKey("Users")).IsTrue();

        var table = schema.Tables["Users"];
        await Assert.That(table.Name).IsEqualTo("Users");
        await Assert.That(table.Columns).Count().IsEqualTo(4);

        // Verify columns
        var idCol = table.Columns.FirstOrDefault(p => p.Name == "Id");
        await Assert.That(idCol).IsNotNull();
        await Assert.That(idCol!.Type).IsEqualTo("INTEGER");
        await Assert.That(idCol.IsPrimaryKey).IsTrue();
        await Assert.That(idCol.IsNullable).IsFalse();

        var nameCol = table.Columns.FirstOrDefault(p => p.Name == "Name");
        await Assert.That(nameCol).IsNotNull();
        await Assert.That(nameCol!.Type).IsEqualTo("TEXT");
        await Assert.That(nameCol.IsPrimaryKey).IsFalse();
        await Assert.That(nameCol.IsNullable).IsFalse();

        var emailCol = table.Columns.FirstOrDefault(p => p.Name == "Email");
        await Assert.That(emailCol).IsNotNull();
        await Assert.That(emailCol!.Type).IsEqualTo("TEXT");
        await Assert.That(emailCol.IsNullable).IsTrue();

        var ageCol = table.Columns.FirstOrDefault(p => p.Name == "Age");
        await Assert.That(ageCol).IsNotNull();
        await Assert.That(ageCol!.Type).IsEqualTo("INTEGER");
        await Assert.That(ageCol.DefaultValue).IsEqualTo("0");
    }

    [Test]
    public async Task ReadSchemaAsync_TableWithIndex_ReadsIndexCorrectly()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE Products (
                              Id INTEGER PRIMARY KEY,
                              Name TEXT NOT NULL,
                              Category TEXT
                          );
                          CREATE INDEX IX_Products_Name ON Products(Name);
                          CREATE UNIQUE INDEX IX_Products_Category ON Products(Category);
                          """;

        await cmd.ExecuteNonQueryAsync();

        var reader = new SqliteSchemaReader(connection);

        // Act
        var schema = await reader.ReadSchemaAsync();

        // Assert
        var table = schema.Tables["Products"];
        await Assert.That(table.Indexes).Count().IsEqualTo(2);

        var nameIndex = table.Indexes.FirstOrDefault(p => p.Name == "IX_Products_Name");
        await Assert.That(nameIndex).IsNotNull();
        await Assert.That(nameIndex!.IsUnique).IsFalse();
        await Assert.That(nameIndex.Columns).Contains("Name");

        var categoryIndex = table.Indexes.FirstOrDefault(p => p.Name == "IX_Products_Category");
        await Assert.That(categoryIndex).IsNotNull();
        await Assert.That(categoryIndex!.IsUnique).IsTrue();
        await Assert.That(categoryIndex.Columns).Contains("Category");
    }

    [Test]
    public async Task ReadSchemaAsync_TableWithForeignKey_ReadsForeignKeyCorrectly()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        // Need to enable foreign keys for this test
        await using var enableFK = connection.CreateCommand();
        enableFK.CommandText = "PRAGMA foreign_keys = ON";
        await enableFK.ExecuteNonQueryAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE Categories (
                              Id INTEGER PRIMARY KEY,
                              Name TEXT NOT NULL
                          );
                          CREATE TABLE Products (
                              Id INTEGER PRIMARY KEY,
                              CategoryId INTEGER NOT NULL,
                              Name TEXT NOT NULL,
                              FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE ON UPDATE NO ACTION
                          );
                          """;

        await cmd.ExecuteNonQueryAsync();

        var reader = new SqliteSchemaReader(connection);

        // Act
        var schema = await reader.ReadSchemaAsync();

        // Assert
        var productsTable = schema.Tables["Products"];
        await Assert.That(productsTable.ForeignKeys).Count().IsEqualTo(1);

        var fk = productsTable.ForeignKeys[0];
        await Assert.That(fk.ColumnName).IsEqualTo("CategoryId");
        await Assert.That(fk.ReferencedTable).IsEqualTo("Categories");
        await Assert.That(fk.ReferencedColumn).IsEqualTo("Id");
        await Assert.That(fk.OnDelete).IsEqualTo("CASCADE");
        await Assert.That(fk.OnUpdate).IsEqualTo("NO ACTION");
    }

    [Test]
    public async Task ReadSchemaAsync_WithIgnoredTables_ExcludesIgnoredTables()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE Users (Id INTEGER PRIMARY KEY);
                          CREATE TABLE __InternalTable (Id INTEGER PRIMARY KEY);
                          CREATE TABLE Products (Id INTEGER PRIMARY KEY);
                          """;

        await cmd.ExecuteNonQueryAsync();

        var options = new SchemaOptions
        {
            IgnoredTables = ["__InternalTable"],
        };

        var reader = new SqliteSchemaReader(connection, options);

        // Act
        var schema = await reader.ReadSchemaAsync();

        // Assert
        await Assert.That(schema.Tables).Count().IsEqualTo(2);
        await Assert.That(schema.Tables.ContainsKey("Users")).IsTrue();
        await Assert.That(schema.Tables.ContainsKey("Products")).IsTrue();
        await Assert.That(schema.Tables.ContainsKey("__InternalTable")).IsFalse();
    }

    [Test]
    public async Task ReadSchemaAsync_WithIgnoredColumns_ExcludesIgnoredColumns()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE Users (
                              Id INTEGER PRIMARY KEY,
                              Name TEXT NOT NULL,
                              RowVersion BLOB
                          )
                          """;

        await cmd.ExecuteNonQueryAsync();

        var options = new SchemaOptions
        {
            IgnoredColumns = new Dictionary<string, HashSet<string>>
            {
                ["Users"] = ["RowVersion"],
            },
        };

        var reader = new SqliteSchemaReader(connection, options);

        // Act
        var schema = await reader.ReadSchemaAsync();

        // Assert
        var table = schema.Tables["Users"];
        await Assert.That(table.Columns).Count().IsEqualTo(2);
        await Assert.That(table.Columns.Any(p => p.Name == "Id")).IsTrue();
        await Assert.That(table.Columns.Any(p => p.Name == "Name")).IsTrue();
        await Assert.That(table.Columns.Any(p => p.Name == "RowVersion")).IsFalse();
    }

    [Test]
    public async Task ReadSchemaAsync_WithCachingEnabled_ReturnsCachedResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)";
        await cmd.ExecuteNonQueryAsync();

        var options = new SchemaOptions { EnableCaching = true };
        var reader = new SqliteSchemaReader(connection, options);

        // Act
        var schema1 = await reader.ReadSchemaAsync();

        // Add another table after first read
        await using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = "CREATE TABLE Products (Id INTEGER PRIMARY KEY)";
        await cmd2.ExecuteNonQueryAsync();

        var schema2 = await reader.ReadSchemaAsync();

        // Assert - should still only have Users because of cache
        await Assert.That(schema1.Tables).Count().IsEqualTo(1);
        await Assert.That(schema2.Tables).Count().IsEqualTo(1);
        await Assert.That(ReferenceEquals(schema1, schema2)).IsTrue();
    }

    [Test]
    public async Task ReadSchemaAsync_WithoutCaching_ReadsLatestSchema()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)";
        await cmd.ExecuteNonQueryAsync();

        var options = new SchemaOptions { EnableCaching = false };
        var reader = new SqliteSchemaReader(connection, options);

        // Act
        var schema1 = await reader.ReadSchemaAsync();

        // Add another table after first read
        await using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = "CREATE TABLE Products (Id INTEGER PRIMARY KEY)";
        await cmd2.ExecuteNonQueryAsync();

        var schema2 = await reader.ReadSchemaAsync();

        // Assert - should have both tables on second read
        await Assert.That(schema1.Tables).Count().IsEqualTo(1);
        await Assert.That(schema2.Tables).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ReadSchemaAsync_CompositePrimaryKey_IdentifiesAllPrimaryKeyColumns()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE OrderItems (
                              OrderId INTEGER NOT NULL,
                              ProductId INTEGER NOT NULL,
                              Quantity INTEGER NOT NULL,
                              PRIMARY KEY (OrderId, ProductId)
                          )
                          """;

        await cmd.ExecuteNonQueryAsync();

        var reader = new SqliteSchemaReader(connection);

        // Act
        var schema = await reader.ReadSchemaAsync();

        // Assert
        var table = schema.Tables["OrderItems"];
        var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();

        await Assert.That(pkColumns).Count().IsEqualTo(2);
        await Assert.That(pkColumns.Any(p => p.Name == "OrderId")).IsTrue();
        await Assert.That(pkColumns.Any(p => p.Name == "ProductId")).IsTrue();
    }

    [Test]
    public async Task ReadSchemaAsync_PreservesColumnOrder()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE Users (
                              Id INTEGER PRIMARY KEY,
                              FirstName TEXT,
                              LastName TEXT,
                              Email TEXT
                          )
                          """;

        await cmd.ExecuteNonQueryAsync();

        var reader = new SqliteSchemaReader(connection);

        // Act
        var schema = await reader.ReadSchemaAsync();

        // Assert
        var table = schema.Tables["Users"];
        await Assert.That(table.Columns[0].Name).IsEqualTo("Id");
        await Assert.That(table.Columns[0].Position).IsEqualTo(0);
        await Assert.That(table.Columns[1].Name).IsEqualTo("FirstName");
        await Assert.That(table.Columns[1].Position).IsEqualTo(1);
        await Assert.That(table.Columns[2].Name).IsEqualTo("LastName");
        await Assert.That(table.Columns[2].Position).IsEqualTo(2);
        await Assert.That(table.Columns[3].Name).IsEqualTo("Email");
        await Assert.That(table.Columns[3].Position).IsEqualTo(3);
    }
}
