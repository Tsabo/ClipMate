using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

[Category("PowerPasteService")]
[Category("Unit")]
public partial class PowerPasteServiceTests
{
    private IConfigurationService _mockConfigService = null!;
    private ILogger<PowerPasteService> _mockLogger = null!;
    private ISoundService _mockSoundService = null!;
    private PowerPasteStatus _status = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = A.Fake<ILogger<PowerPasteService>>();
        _mockConfigService = A.Fake<IConfigurationService>();
        _mockSoundService = A.Fake<ISoundService>();
        _status = new PowerPasteStatus();

        // Configure sound service to return completed task (with or without cancellation token)
        A.CallTo(() => _mockSoundService.PlaySoundAsync(A<SoundEvent>.Ignored))
            .Returns(Task.CompletedTask);

        A.CallTo(() => _mockSoundService.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored))
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

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);
    }

    private PowerPasteService CreateService(TimeProvider? timeProvider = null) => new(
        _mockConfigService,
        _mockSoundService,
        _mockLogger,
        _status,
        timeProvider);

    private void UseConfiguration(PreferencesConfiguration preferences) =>
        A.CallTo(() => _mockConfigService.Configuration).Returns(new ClipMateConfiguration { Preferences = preferences });

    private int CompleteSoundCount() =>
        Fake.GetCalls(_mockSoundService).Count(p => p.Method.Name == nameof(ISoundService.PlaySoundAsync) && Equals(p.Arguments[0], SoundEvent.PowerPasteComplete));

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
