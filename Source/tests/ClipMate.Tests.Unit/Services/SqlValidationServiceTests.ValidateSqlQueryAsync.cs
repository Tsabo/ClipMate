using ClipMate.Data;

namespace ClipMate.Tests.Unit.Services;

public partial class SqlValidationServiceTests
{
    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithEmptyQuery_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("cannot be empty");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithWhitespaceQuery_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("   ", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("cannot be empty");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithDropKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("DROP TABLE Clips", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("DROP");
        await Assert.That(errorMessage).Contains("dangerous keyword");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithDeleteKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("DELETE FROM Clips WHERE Id = 1", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("DELETE");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithUpdateKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("UPDATE Clips SET Title = 'Test'", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("UPDATE");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithInsertKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("INSERT INTO Clips VALUES (1, 'test')", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("INSERT");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithAlterKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("ALTER TABLE Clips ADD COLUMN Test TEXT", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("ALTER");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithCreateKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("CREATE TABLE Test (Id INT)", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("CREATE");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithTruncateKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("TRUNCATE TABLE Clips", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("TRUNCATE");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithExecKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("EXEC sp_something", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("EXEC");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithExecuteKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("EXECUTE sp_something", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("EXEC"); // Service returns "EXEC" not "EXECUTE" from dangerous keywords array
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithPragmaKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("PRAGMA table_info(Clips)", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("PRAGMA");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_IsCaseInsensitive()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("drop table Clips", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("DROP");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithMixedCaseKeyword_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("DeLeTe FROM Clips", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("DELETE");
    }

    [Test]
    [Category("ValidateSqlQueryAsync")]
    public async Task ValidateSqlQueryAsync_WithDatabaseNotLoaded_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();
        _mockDatabaseManager.Setup(p => p.CreateDatabaseContext(_testDatabaseKey))
            .Returns((ClipMateDbContext?)null);

        // Act
        var (isValid, errorMessage) = await service.ValidateSqlQueryAsync("SELECT * FROM Clips", _testDatabaseKey);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("is not loaded");
    }
}
