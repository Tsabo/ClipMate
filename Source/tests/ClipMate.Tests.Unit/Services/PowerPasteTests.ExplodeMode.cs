using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteTests
{
    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithNewlineDelimiter_SplitsClipCorrectly()
    {
        // Arrange
        var service = CreateService();
        var clip = CreateTestClip("Line1\nLine2\nLine3");

        // Act
        UseExplode();
        await service.StartAsync([clip], PowerPasteDirection.Down);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(3);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Line1");
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

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("Apple,Banana,Cherry");

        // Act
        UseExplode();
        await service.StartAsync([clip], PowerPasteDirection.Down);

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

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("  Apple  ,  Banana  ,  Cherry  ");

        // Act
        UseExplode();
        await service.StartAsync([clip], PowerPasteDirection.Down);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(3);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Apple");
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

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("  Apple  ,  Banana  ");

        // Act
        UseExplode();
        await service.StartAsync([clip], PowerPasteDirection.Down);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(2);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("  Apple  ");
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

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("Apple,,,Banana");

        // Act
        UseExplode();
        await service.StartAsync([clip], PowerPasteDirection.Down);

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

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        var service = CreateService();
        var clip = CreateTestClip("Column1\tColumn2\tColumn3");

        // Act
        UseExplode();
        await service.StartAsync([clip], PowerPasteDirection.Down);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(3);
        await Assert.That(WaitingClip(service)?.TextContent).IsEqualTo("Column1");
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_WithEmptyClip_ReturnsOriginalClip()
    {
        // Arrange
        var service = CreateService();
        var clip = CreateTestClip("");

        // Act
        UseExplode();
        await service.StartAsync([clip], PowerPasteDirection.Down);

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

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        var service = CreateService();
        var clips = new[]
        {
            CreateTestClip("A,B"),
            CreateTestClip("C,D,E"),
        };

        // Act
        UseExplode();
        await service.StartAsync(clips, PowerPasteDirection.Down);

        // Assert
        await Assert.That(service.TotalCount).IsEqualTo(5); // A, B, C, D, E
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_IncludeDelimiterWithARepeatedPiece_KeepsEachPiecesOwnDelimiter()
    {
        // Arrange - the second "a" ends at a comma; finding it by its text would give it the first one's semicolon
        UseConfiguration(new PreferencesConfiguration
        {
            PowerPasteExplode = true,
            PowerPasteDelimiter = ",;",
            PowerPasteIncludeDelimiter = true,
        });

        var service = CreateService();

        // Act
        await service.StartAsync([CreateTestClip("a;b,a,c")], PowerPasteDirection.Down);

        // Assert
        await Assert.That(await PastedPiecesAsync(service, 4)).IsEquivalentTo(["a;", "b,", "a,", "c"]);
    }

    [Test]
    [Category("ExplodeMode")]
    public async Task ExplodeMode_IncludeDelimiterWithTrim_KeepsTheDelimiterAfterTheTrimmedPiece()
    {
        // Arrange - the spaces before the comma are trimmed, and the comma is still kept
        UseConfiguration(new PreferencesConfiguration
        {
            PowerPasteExplode = true,
            PowerPasteDelimiter = ",",
            PowerPasteTrim = true,
            PowerPasteIncludeDelimiter = true,
        });

        var service = CreateService();

        // Act
        await service.StartAsync([CreateTestClip("Apple  , Banana")], PowerPasteDirection.Down);

        // Assert
        await Assert.That(await PastedPiecesAsync(service, 2)).IsEquivalentTo(["Apple,", "Banana"]);
    }

    /// <summary>
    /// Pastes <paramref name="count" /> times and returns the text of each piece as it waited to be pasted.
    /// </summary>
    private async Task<List<string>> PastedPiecesAsync(IPowerPaste service, int count)
    {
        var pieces = new List<string>();
        for (var i = 0; i < count; i++)
        {
            pieces.Add(WaitingClip(service)?.TextContent ?? "(none)");
            await PasteAsync();
        }

        return pieces;
    }
}
