using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteServiceTests
{
    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithNewlineDelimiter_SplitsClipCorrectly()
    {
        // Arrange
        var service = CreateService();
        var clip = CreateTestClip("Line1\nLine2\nLine3");

        // Act
        await service.StartAsync([clip], PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(3);
        await Assert.That(service.GetCurrentClip()?.TextContent).IsEqualTo("Line1");
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithCustomDelimiter_SplitsCorrectly()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                PowerPasteDelimiter = ",",
                PowerPasteTrim = true,
            },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("Apple,Banana,Cherry");

        // Act
        await service.StartAsync([clip], PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(3);
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithTrimEnabled_TrimsFragments()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                PowerPasteDelimiter = ",",
                PowerPasteTrim = true,
            },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("  Apple  ,  Banana  ,  Cherry  ");

        // Act
        await service.StartAsync([clip], PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(3);
        await Assert.That(service.GetCurrentClip()?.TextContent).IsEqualTo("Apple");
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithTrimDisabled_PreservesWhitespace()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                PowerPasteDelimiter = ",",
                PowerPasteTrim = false,
            },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("  Apple  ,  Banana  ");

        // Act
        await service.StartAsync([clip], PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(2);
        await Assert.That(service.GetCurrentClip()?.TextContent).IsEqualTo("  Apple  ");
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithEmptyFragments_SkipsThem()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                PowerPasteDelimiter = ",",
                PowerPasteTrim = true,
            },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("Apple,,,Banana");

        // Act
        await service.StartAsync([clip], PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(2);
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithTabDelimiter_SplitsCorrectly()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                PowerPasteDelimiter = "\\t",
                PowerPasteTrim = false,
            },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("Column1\tColumn2\tColumn3");

        // Act
        await service.StartAsync([clip], PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(3);
        await Assert.That(service.GetCurrentClip()?.TextContent).IsEqualTo("Column1");
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithEmptyClip_ReturnsOriginalClip()
    {
        // Arrange
        var service = CreateService();
        var clip = CreateTestClip("");

        // Act
        await service.StartAsync([clip], PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(1);
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithMultipleClips_ExplodesAll()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                PowerPasteDelimiter = ",",
                PowerPasteTrim = true,
            },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        var service = CreateService();
        var clips = new[]
        {
            CreateTestClip("A,B"),
            CreateTestClip("C,D,E"),
        };

        // Act
        await service.StartAsync(clips, PowerPasteDirection.Down, true);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(5); // A, B, C, D, E
    }
}
