using ClipMate.Core.Models.Configuration;
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
    protected ClipMateConfiguration Configuration = null!;
    protected ILogger<ApplicationProfileService> MockLogger = null!;
    protected IApplicationProfileStore MockStore = null!;
    protected ApplicationProfileService Service = null!;

    [Before(Test)]
    public Task SetupAsync()
    {
        MockLogger = A.Fake<ILogger<ApplicationProfileService>>();
        MockStore = A.Fake<IApplicationProfileStore>(o => o.Strict());
        Configuration = new ClipMateConfiguration();
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(Configuration);
        Service = new ApplicationProfileService(MockStore, configurationService, MockLogger);

        return Task.CompletedTask;
    }
}
