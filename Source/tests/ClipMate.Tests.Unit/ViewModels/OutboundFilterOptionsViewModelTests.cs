using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The Outbound Clip Filtering section of the Pasting options page.
/// </summary>
public class OutboundFilterOptionsViewModelTests
{
    private ClipMateConfiguration _configuration = null!;
    private IConfigurationService _configurationService = null!;
    private ICurrentClipWriter _writer = null!;

    [Before(Test)]
    public void Setup()
    {
        _configuration = new ClipMateConfiguration();
        _configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => _configurationService.Configuration).Returns(_configuration);
        _writer = A.Fake<ICurrentClipWriter>();
    }

    [Test]
    public async Task Defaults_AreClipMate7s()
    {
        // Assert: off, Passive, Explorer excluded (OBFilter, OBFilterActive71, FilterExclusionList)
        var preferences = new PreferencesConfiguration();
        await Assert.That(preferences.OutboundFilter).IsFalse();
        await Assert.That(preferences.OutboundFilterMode).IsEqualTo(OutboundFilterMode.Passive);
        await Assert.That(preferences.OutboundFilterExclusions).IsEquivalentTo(new[] { "EXPLORER" });
    }

    [Test]
    public async Task Save_ChangedSettings_AreSaved()
    {
        // Arrange
        var viewModel = new OutboundFilterOptionsViewModel(_configurationService, _writer, _ => "WINWORD");
        viewModel.Load();

        // Act
        viewModel.IsEnabled = true;
        viewModel.Mode = OutboundFilterMode.Active;
        viewModel.Exclusions.AddCommand.Execute(null);
        viewModel.Save();

        // Assert
        await Assert.That(_configuration.Preferences.OutboundFilter).IsTrue();
        await Assert.That(_configuration.Preferences.OutboundFilterMode).IsEqualTo(OutboundFilterMode.Active);
        await Assert.That(_configuration.Preferences.OutboundFilterExclusions).IsEquivalentTo(new[] { "EXPLORER", "WINWORD" });
    }

    [Test]
    public async Task AddExclusion_StartsFromTheCurrentClipsSourceApplication()
    {
        // Arrange: as ClipMate 7's Add button does
        A.CallTo(() => _writer.CurrentClip).Returns(new CurrentClip(new Clip { SourceApplicationName = "FIREFOX" }, "db"));
        string? offered = null;
        var viewModel = new OutboundFilterOptionsViewModel(_configurationService, _writer, current =>
        {
            offered = current;
            return null;
        });

        // Act
        viewModel.Exclusions.AddCommand.Execute(null);

        // Assert
        await Assert.That(offered).IsEqualTo("FIREFOX");
    }

    [Test]
    public async Task ResetToDefaults_RestoresClipMate7sDefaults()
    {
        // Arrange
        _configuration.Preferences.OutboundFilter = true;
        _configuration.Preferences.OutboundFilterMode = OutboundFilterMode.Active;
        _configuration.Preferences.OutboundFilterExclusions = [];
        var viewModel = new OutboundFilterOptionsViewModel(_configurationService, _writer, _ => null);
        viewModel.Load();

        // Act
        viewModel.ResetToDefaults();

        // Assert
        await Assert.That(viewModel.IsEnabled).IsFalse();
        await Assert.That(viewModel.Mode).IsEqualTo(OutboundFilterMode.Passive);
        await Assert.That(viewModel.Exclusions.Items).IsEquivalentTo(new[] { "EXPLORER" });
    }
}
