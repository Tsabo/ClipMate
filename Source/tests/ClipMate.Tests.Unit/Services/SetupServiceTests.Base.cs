using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class SetupServiceTests
{
    private SetupService CreateService()
    {
        var mockLogger = A.Fake<ILogger<SetupService>>();
        return new SetupService(mockLogger);
    }
}
