using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

[Category("SqlValidationService")]
[Category("Unit")]
public partial class SqlValidationServiceTests
{
    private const string _testDatabaseKey = "test-db";
    private Mock<IDatabaseManager> _mockDatabaseManager = null!;
    private Mock<ILogger<SqlValidationService>> _mockLogger = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<SqlValidationService>>();
        _mockDatabaseManager = new Mock<IDatabaseManager>();
    }

    private SqlValidationService CreateService() => new(
        _mockDatabaseManager.Object,
        _mockLogger.Object);
}
