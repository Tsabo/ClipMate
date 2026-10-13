using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

[Category("SqlValidationService")]
[Category("Unit")]
public partial class SqlValidationServiceTests
{
    private const string _testDatabaseKey = "test-db";
    private IDatabaseManager _mockDatabaseManager = null!;
    private ILogger<SqlValidationService> _mockLogger = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = A.Fake<ILogger<SqlValidationService>>();
        _mockDatabaseManager = A.Fake<IDatabaseManager>();
    }

    private SqlValidationService CreateService() => new(
        _mockDatabaseManager,
        _mockLogger);
}
