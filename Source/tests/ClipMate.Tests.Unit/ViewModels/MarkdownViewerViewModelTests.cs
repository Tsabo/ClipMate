using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for MarkdownViewerViewModel.
/// </summary>
[Category("ViewModel")]
public class MarkdownViewerViewModelTests : TestFixtureBase
{
    private const string _cfHtml = "<!--StartFragment--><b>Html</b><!--EndFragment-->";
    private const string _rtf = @"{\rtf1 {\b Rtf}}";

    private IMarkdownConversionService _markdownConversionService = null!;
    private IRtfToHtmlConverter _rtfToHtmlConverter = null!;
    private IConfigurationService _configurationService = null!;
    private IClipService _clipService = null!;
    private IMessenger _messenger = null!;
    private ClipMateConfiguration _configuration = null!;

    [Before(Test)]
    public void Setup()
    {
        _markdownConversionService = A.Fake<IMarkdownConversionService>();
        _rtfToHtmlConverter = A.Fake<IRtfToHtmlConverter>();
        _configurationService = A.Fake<IConfigurationService>();
        _clipService = A.Fake<IClipService>();
        _messenger = new StrongReferenceMessenger();

        _configuration = new ClipMateConfiguration();
        A.CallTo(() => _configurationService.Configuration).Returns(_configuration);

        A.CallTo(() => _rtfToHtmlConverter.ConvertToHtml(A<string>._)).Returns("<b>FromRtf</b>");
        A.CallTo(() => _markdownConversionService.ConvertHtmlToMarkdown(A<string>._, A<MarkdownConversionOptions>._))
            .ReturnsLazily((string html, MarkdownConversionOptions options) => $"md:{options.Flavor}:{html}");
    }

    private MarkdownViewerViewModel CreateViewModel() =>
        new(_markdownConversionService, _rtfToHtmlConverter, _configurationService, _clipService, _messenger,
            CreateLogger<MarkdownViewerViewModel>());

    [Test]
    public async Task Constructor_LoadsPreferences()
    {
        // Arrange
        _configuration.Preferences.MarkdownFlavor = MarkdownFlavor.Slack;
        _configuration.Preferences.MarkdownRemoveComments = false;
        _configuration.Preferences.MarkdownSmartHref = false;
        _configuration.Preferences.MarkdownShowPreview = true;

        // Act
        var viewModel = CreateViewModel();

        // Assert
        await Assert.That(viewModel.SelectedFlavor).IsEqualTo(MarkdownFlavor.Slack);
        await Assert.That(viewModel.RemoveComments).IsFalse();
        await Assert.That(viewModel.SmartHref).IsFalse();
        await Assert.That(viewModel.IsPreviewMode).IsTrue();
        A.CallTo(() => _configurationService.SaveAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task SetSource_BothFormats_PrefersHtmlAndShowsSelector()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SetSource(_cfHtml, _rtf, "db", Guid.NewGuid(), "Title");

        // Assert
        await Assert.That(viewModel.HasSource).IsTrue();
        await Assert.That(viewModel.HasMultipleSources).IsTrue();
        await Assert.That(viewModel.SelectedSource).IsEqualTo(MarkdownSourceFormat.Html);
    }

    [Test]
    public async Task SetSource_RtfOnly_SelectsRtf()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SetSource(null, _rtf, "db", Guid.NewGuid(), "Title");

        // Assert
        await Assert.That(viewModel.HasMultipleSources).IsFalse();
        await Assert.That(viewModel.SelectedSource).IsEqualTo(MarkdownSourceFormat.Rtf);
    }

    [Test]
    public async Task SetSource_WhileInactive_DoesNotConvert()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SetSource(_cfHtml, null, "db", Guid.NewGuid(), "Title");

        // Assert
        await Assert.That(viewModel.MarkdownText).IsEmpty();
        A.CallTo(() => _markdownConversionService.ConvertHtmlToMarkdown(A<string>._, A<MarkdownConversionOptions>._))
            .MustNotHaveHappened();
    }

    [Test]
    public async Task IsActive_Html_ConvertsFragment()
    {
        // Arrange
        _configuration.Preferences.MarkdownFlavor = MarkdownFlavor.GitHub;
        var viewModel = CreateViewModel();
        viewModel.SetSource(_cfHtml, _rtf, "db", Guid.NewGuid(), "Title");

        // Act
        viewModel.IsActive = true;

        // Assert
        await Assert.That(viewModel.MarkdownText).IsEqualTo("md:GitHub:<b>Html</b>");
        A.CallTo(() => _rtfToHtmlConverter.ConvertToHtml(A<string>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task SelectedSource_ChangedToRtf_ConvertsRtf()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.SetSource(_cfHtml, _rtf, "db", Guid.NewGuid(), "Title");
        viewModel.IsActive = true;

        // Act
        viewModel.SelectedSource = MarkdownSourceFormat.Rtf;

        // Assert
        await Assert.That(viewModel.MarkdownText).IsEqualTo("md:GitHub:<b>FromRtf</b>");
        A.CallTo(() => _rtfToHtmlConverter.ConvertToHtml(_rtf)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SelectedFlavor_Changed_ReconvertsAndSavesPreference()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.SetSource(_cfHtml, null, "db", Guid.NewGuid(), "Title");
        viewModel.IsActive = true;

        // Act
        viewModel.SelectedFlavor = MarkdownFlavor.Slack;

        // Assert
        await Assert.That(viewModel.MarkdownText).IsEqualTo("md:Slack:<b>Html</b>");
        await Assert.That(_configuration.Preferences.MarkdownFlavor).IsEqualTo(MarkdownFlavor.Slack);
        A.CallTo(() => _configurationService.SaveAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task IsPreviewMode_Enabled_RendersPreviewHtml()
    {
        // Arrange
        A.CallTo(() => _markdownConversionService.ConvertHtmlToMarkdown(A<string>._, A<MarkdownConversionOptions>._))
            .Returns("# Heading");
        var viewModel = CreateViewModel();
        viewModel.SetSource(_cfHtml, null, "db", Guid.NewGuid(), "Title");
        viewModel.IsActive = true;

        // Act
        viewModel.IsPreviewMode = true;

        // Assert
        await Assert.That(viewModel.PreviewHtml).Contains("<h1");
        await Assert.That(viewModel.PreviewHtml).Contains("Heading</h1>");
    }

    [Test]
    public async Task ConversionFailure_ShowsErrorComment()
    {
        // Arrange
        A.CallTo(() => _rtfToHtmlConverter.ConvertToHtml(A<string>._)).Throws(new InvalidOperationException("bad rtf"));
        var viewModel = CreateViewModel();
        viewModel.SetSource(null, _rtf, "db", Guid.NewGuid(), "Title");

        // Act
        viewModel.IsActive = true;

        // Assert
        await Assert.That(viewModel.MarkdownText).Contains("bad rtf");
    }

    [Test]
    public async Task Clear_RemovesSourceAndOutput()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.SetSource(_cfHtml, null, "db", Guid.NewGuid(), "Title");
        viewModel.IsActive = true;

        // Act
        viewModel.Clear();

        // Assert
        await Assert.That(viewModel.HasSource).IsFalse();
        await Assert.That(viewModel.MarkdownText).IsEmpty();
    }

    [Test]
    public async Task SaveAsNewClipCommand_CreatesTextClipInCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        Clip? savedClip = null;
        A.CallTo(() => _clipService.CreateAsync("db", A<Clip>._, A<CancellationToken>._))
            .ReturnsLazily((string _, Clip clip, CancellationToken _) =>
            {
                savedClip = clip;
                return clip;
            });

        var reloadRequested = false;
        _messenger.Register<ReloadClipsRequestedEvent>(this, (_, _) => reloadRequested = true);

        var viewModel = CreateViewModel();
        viewModel.SetSource(_cfHtml, null, "db", collectionId, "Web Page");
        viewModel.IsActive = true;

        // Act
        await viewModel.SaveAsNewClipCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(savedClip).IsNotNull();
        await Assert.That(savedClip!.TextContent).IsEqualTo(viewModel.MarkdownText);
        await Assert.That(savedClip.CollectionId).IsEqualTo(collectionId);
        await Assert.That(savedClip.Title).IsEqualTo("Web Page (Markdown)");
        await Assert.That(savedClip.Type).IsEqualTo(ClipType.Text);
        await Assert.That(savedClip.ContentHash).IsNotEmpty();
        await Assert.That(reloadRequested).IsTrue();
    }

    [Test]
    public async Task CopyCommand_NoMarkdown_CannotExecute()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act & Assert
        await Assert.That(viewModel.CopyCommand.CanExecute(null)).IsFalse();
        await Assert.That(viewModel.SaveAsNewClipCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task CopyCommand_WithMarkdown_InvokesCopyCallback()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var copyRequested = false;
        viewModel.OnCopyRequested = () => copyRequested = true;
        viewModel.SetSource(_cfHtml, null, "db", Guid.NewGuid(), "Title");
        viewModel.IsActive = true;

        // Act
        viewModel.CopyCommand.Execute(null);

        // Assert
        await Assert.That(copyRequested).IsTrue();
    }
}
