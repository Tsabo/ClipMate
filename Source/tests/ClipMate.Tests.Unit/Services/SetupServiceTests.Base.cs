using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class SetupServiceTests
{
    private SetupService CreateService()
    {
        var mockLogger = new Mock<ILogger<SetupService>>();
        return new SetupService(mockLogger.Object);
    }
}
