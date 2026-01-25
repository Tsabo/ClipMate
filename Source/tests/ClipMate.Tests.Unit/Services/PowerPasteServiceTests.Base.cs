using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

[Category("PowerPasteService")]
[Category("Unit")]
public partial class PowerPasteServiceTests
{
    private Mock<IConfigurationService> _mockConfigService = null!;
    private Mock<ILogger<PowerPasteService>> _mockLogger = null!;
    private Mock<ISoundService> _mockSoundService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<PowerPasteService>>();
        _mockConfigService = new Mock<IConfigurationService>();
        _mockSoundService = new Mock<ISoundService>();

        // Configure sound service to return completed task (with or without cancellation token)
        _mockSoundService.Setup(p => p.PlaySoundAsync(It.IsAny<SoundEvent>()))
            .Returns(Task.CompletedTask);

        _mockSoundService.Setup(p => p.PlaySoundAsync(It.IsAny<SoundEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default configuration
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                PowerPasteLoop = false,
                PowerPasteDelimiter = "\n",
                PowerPasteTrim = true,
                PowerPasteIncludeDelimiter = false,
            },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);
    }

    private PowerPasteService CreateService() => new(
        _mockConfigService.Object,
        _mockSoundService.Object,
        _mockLogger.Object);

    private static Clip CreateTestClip(string textContent,
        Guid? id = null,
        Guid? collectionId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TextContent = textContent,
        Type = ClipType.Text,
        CapturedAt = DateTimeOffset.Now,
        CollectionId = collectionId ?? Guid.NewGuid(),
        Title = "Test Clip",
        SourceApplicationName = "TestApp",
    };
}
