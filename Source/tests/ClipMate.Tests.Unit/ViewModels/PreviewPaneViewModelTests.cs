using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Events;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

public class PreviewPaneViewModelTests
{
    [Test]
    public async Task Constructor_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var mockMessenger = new Mock<IMessenger>();
        var viewModel = new PreviewPaneViewModel(mockMessenger.Object);

        // Assert
        await Assert.That(viewModel.SelectedClip).IsNull();
        await Assert.That(viewModel.PreviewText).IsEmpty();
        await Assert.That(viewModel.PreviewHtml).IsEmpty();
        await Assert.That(viewModel.PreviewImageSource).IsNull();
        await Assert.That(viewModel.HasTextPreview).IsFalse();
        await Assert.That(viewModel.HasHtmlPreview).IsFalse();
        await Assert.That(viewModel.HasImagePreview).IsFalse();
    }

    [Test]
    public async Task Receive_WithTextClip_ShouldSetPreviewText()
    {
        // Arrange
        var mockMessenger = new Mock<IMessenger>();
        var viewModel = new PreviewPaneViewModel(mockMessenger.Object);
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test content",
            CapturedAt = DateTime.UtcNow
        };

        // Act
        viewModel.Receive(new ClipSelectedEvent(clip));

        // Assert
        await Assert.That(viewModel.SelectedClip).IsEqualTo(clip);
        await Assert.That(viewModel.PreviewText).IsEqualTo("Test content");
        await Assert.That(viewModel.HasTextPreview).IsTrue();
        await Assert.That(viewModel.HasHtmlPreview).IsFalse();
        await Assert.That(viewModel.HasImagePreview).IsFalse();
    }

    [Test]
    public async Task Receive_WithHtmlClip_ShouldSetPreviewHtml()
    {
        // Arrange
        var mockMessenger = new Mock<IMessenger>();
        var viewModel = new PreviewPaneViewModel(mockMessenger.Object);
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Html,
            HtmlContent = "<h1>Test</h1>",
            TextContent = "Test",
            CapturedAt = DateTime.UtcNow
        };

        // Act
        viewModel.Receive(new ClipSelectedEvent(clip));

        // Assert
        await Assert.That(viewModel.SelectedClip).IsEqualTo(clip);
        await Assert.That(viewModel.PreviewHtml).IsEqualTo("<h1>Test</h1>");
        await Assert.That(viewModel.HasHtmlPreview).IsTrue();
        await Assert.That(viewModel.HasTextPreview).IsFalse();
        await Assert.That(viewModel.HasImagePreview).IsFalse();
    }

    [Test]
    public async Task Receive_WithRichTextClip_ShouldSetPreviewText()
    {
        // Arrange
        var mockMessenger = new Mock<IMessenger>();
        var viewModel = new PreviewPaneViewModel(mockMessenger.Object);
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.RichText,
            RtfContent = @"{\rtf1\ansi Test RTF}",
            TextContent = "Test RTF",
            CapturedAt = DateTime.UtcNow
        };

        // Act
        viewModel.Receive(new ClipSelectedEvent(clip));

        // Assert
        await Assert.That(viewModel.SelectedClip).IsEqualTo(clip);
        await Assert.That(viewModel.PreviewText).IsEqualTo("Test RTF");
        await Assert.That(viewModel.HasTextPreview).IsTrue();
    }

    [Test]
    public async Task Receive_WithImageClip_ShouldSetImagePreview()
    {
        // Arrange
        var mockMessenger = new Mock<IMessenger>();
        var viewModel = new PreviewPaneViewModel(mockMessenger.Object);
        // Note: Creating a valid image in memory is complex for unit tests
        // We test the logic path, but image loading may fail with invalid data
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header bytes (not a complete image)
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Image,
            ImageData = imageData,
            CapturedAt = DateTime.UtcNow
        };

        // Act
        viewModel.Receive(new ClipSelectedEvent(clip));

        // Assert - We set HasImagePreview even if image loading fails
        await Assert.That(viewModel.SelectedClip).IsEqualTo(clip);
        await Assert.That(viewModel.HasImagePreview).IsTrue();
        await Assert.That(viewModel.HasTextPreview).IsFalse();
        await Assert.That(viewModel.HasHtmlPreview).IsFalse();
        // Note: PreviewImageSource may be null if image data is invalid, which is OK for this test
    }

    [Test]
    public async Task Receive_WithNull_ShouldClearPreview()
    {
        // Arrange
        var mockMessenger = new Mock<IMessenger>();
        var viewModel = new PreviewPaneViewModel(mockMessenger.Object);
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test",
            CapturedAt = DateTime.UtcNow
        };
        viewModel.Receive(new ClipSelectedEvent(clip));

        // Act
        viewModel.Receive(new ClipSelectedEvent(null));

        // Assert
        await Assert.That(viewModel.SelectedClip).IsNull();
        await Assert.That(viewModel.PreviewText).IsEmpty();
        await Assert.That(viewModel.PreviewHtml).IsEmpty();
        await Assert.That(viewModel.PreviewImageSource).IsNull();
        await Assert.That(viewModel.HasTextPreview).IsFalse();
        await Assert.That(viewModel.HasHtmlPreview).IsFalse();
        await Assert.That(viewModel.HasImagePreview).IsFalse();
    }

    [Test]
    public async Task SelectedClip_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var mockMessenger = new Mock<IMessenger>();
        var viewModel = new PreviewPaneViewModel(mockMessenger.Object);
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test",
            CapturedAt = DateTime.UtcNow
        };
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PreviewPaneViewModel.SelectedClip))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.Receive(new ClipSelectedEvent(clip));

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
    }

    [Test]
    public async Task Receive_WithNullClip_ShouldClearAllPreviewData()
    {
        // Arrange
        var mockMessenger = new Mock<IMessenger>();
        var viewModel = new PreviewPaneViewModel(mockMessenger.Object);
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test",
            CapturedAt = DateTime.UtcNow
        };
        viewModel.Receive(new ClipSelectedEvent(clip));

        // Act
        viewModel.Receive(new ClipSelectedEvent(null));

        // Assert
        await Assert.That(viewModel.SelectedClip).IsNull();
        await Assert.That(viewModel.PreviewText).IsEmpty();
        await Assert.That(viewModel.PreviewHtml).IsEmpty();
        await Assert.That(viewModel.PreviewImageSource).IsNull();
        await Assert.That(viewModel.HasTextPreview).IsFalse();
        await Assert.That(viewModel.HasHtmlPreview).IsFalse();
        await Assert.That(viewModel.HasImagePreview).IsFalse();
    }
}
