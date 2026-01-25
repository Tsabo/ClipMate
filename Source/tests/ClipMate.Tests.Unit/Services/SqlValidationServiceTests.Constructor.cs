using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class SqlValidationServiceTests
{
    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullDatabaseManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new SqlValidationService(
            null!,
            _mockLogger.Object));

        await Assert.That(exception.ParamName).IsEqualTo("databaseManager");
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new SqlValidationService(
            _mockDatabaseManager.Object,
            null!));

        await Assert.That(exception.ParamName).IsEqualTo("logger");
    }
}
