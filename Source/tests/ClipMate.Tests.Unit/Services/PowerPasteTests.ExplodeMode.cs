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

    [Test]
    [Category("FragmentSpan")]
    public async Task FragmentSpans_OfRepeatedFragments_AreEachTheirOwn()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelimiter = ",", PowerPasteTrim = true });
        UseExplode();
        var service = CreateService();

        // Act
        var spans = await FragmentSpansAsync(service, "ab,ab,ab", 3);

        // Assert
        await Assert.That(spans).IsEquivalentTo([(0, 2), (3, 2), (6, 2)]);
    }

    [Test]
    [Category("FragmentSpan")]
    public async Task FragmentSpans_OfTrimmedFragments_CoverOnlyTheText()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelimiter = ",", PowerPasteTrim = true });
        UseExplode();
        var service = CreateService();

        // Act
        var spans = await FragmentSpansAsync(service, "  a ,  b", 2);

        // Assert
        await Assert.That(spans).IsEquivalentTo([(2, 1), (7, 1)]);
    }

    [Test]
    [Category("FragmentSpan")]
    public async Task FragmentSpans_WithTheDelimiterIncluded_RunOnToTheDelimiter()
    {
        // Arrange
        UseConfiguration(new PreferencesConfiguration { PowerPasteDelimiter = ",", PowerPasteTrim = true, PowerPasteIncludeDelimiter = true });
        UseExplode();
        var service = CreateService();

        // Act
        var spans = await FragmentSpansAsync(service, "a ,b", 2);

        // Assert
        await Assert.That(spans).IsEquivalentTo([(0, 3), (3, 1)]);
    }

    [Test]
    [Category("FragmentSpan")]
    public async Task FragmentSpans_WithoutExplode_AreNotReported()
    {
        // Arrange
        var service = CreateService();
        PowerPastePositionChangedEventArgs? position = null;
        service.PositionChanged += (_, e) => position = e;

        // Act
        await service.StartAsync([CreateTestClip("a,b")], PowerPasteDirection.Down);

        // Assert
        await Assert.That(position?.IsExplodePiece).IsFalse();
        await Assert.That(position?.FragmentLength).IsEqualTo(0);
    }

    /// <summary>
    /// Starts Power Paste on one clip of <paramref name="text" />, pastes <paramref name="count" /> times, and returns
    /// where each Fragment lay in the text as it waited to be pasted.
    /// </summary>
    private async Task<List<(int Start, int Length)>> FragmentSpansAsync(IPowerPaste service, string text, int count)
    {
        var spans = new List<(int Start, int Length)>();
        service.PositionChanged += (_, e) =>
        {
            if (e.IsExplodePiece)
                spans.Add((e.FragmentStart, e.FragmentLength));
        };

        await service.StartAsync([CreateTestClip(text)], PowerPasteDirection.Down);
        for (var i = 1; i < count; i++)
            await PasteAsync();

        return spans;
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
