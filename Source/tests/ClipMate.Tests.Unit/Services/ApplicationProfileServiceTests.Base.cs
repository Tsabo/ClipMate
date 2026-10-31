using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base setup for ApplicationProfileService tests.
/// </summary>
public abstract class ApplicationProfileServiceTestsBase
{
    protected ILogger<ApplicationProfileService> MockLogger = null!;
    protected IApplicationProfileStore MockStore = null!;
    protected ApplicationProfileService Service = null!;

    [Before(Test)]
    public Task SetupAsync()
    {
        MockLogger = A.Fake<ILogger<ApplicationProfileService>>();
        MockStore = A.Fake<IApplicationProfileStore>(o => o.Strict());
        Service = new ApplicationProfileService(MockStore, MockLogger);

        return Task.CompletedTask;
    }
}
