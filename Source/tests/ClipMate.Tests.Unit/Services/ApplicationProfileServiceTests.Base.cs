using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base setup for ApplicationProfileService tests.
/// </summary>
public abstract class ApplicationProfileServiceTestsBase
{
    protected Mock<ILogger<ApplicationProfileService>> MockLogger = null!;
    protected Mock<IApplicationProfileStore> MockStore = null!;
    protected ApplicationProfileService Service = null!;

    [Before(Test)]
    public Task SetupAsync()
    {
        MockLogger = new Mock<ILogger<ApplicationProfileService>>();
        MockStore = new Mock<IApplicationProfileStore>(MockBehavior.Strict);
        Service = new ApplicationProfileService(MockStore.Object, MockLogger.Object);

        return Task.CompletedTask;
    }
}
