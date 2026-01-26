using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class SoundServiceTestsBase
{
    protected Mock<IConfigurationService> ConfigService { get; private set; } = null!;
    protected Mock<ILogger<SoundService>> Logger { get; private set; } = null!;
    protected ClipMateConfiguration Configuration { get; private set; } = null!;

    [Before(Test)]
    public void Setup()
    {
        ConfigService = new Mock<IConfigurationService>();
        Logger = new Mock<ILogger<SoundService>>();

        // Create default configuration with sounds disabled
        Configuration = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                Sound = new SoundConfiguration
                {
                    ClipboardUpdate = SoundMode.Off,
                    Append = SoundMode.Off,
                    Erase = SoundMode.Off,
                    Filter = SoundMode.Off,
                    Ignore = SoundMode.Off,
                    PowerPasteComplete = SoundMode.Off,
                },
            },
        };

        ConfigService.Setup(p => p.Configuration).Returns(Configuration);
    }

    protected SoundService CreateService() => new(ConfigService.Object, Logger.Object);
}
