using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Events;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Shouldly;

namespace ClipMate.Tests.Unit.ViewModels;

public class PreviewPaneViewModelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var mockMessenger = new Mock<IMessenger>();
        var viewModel = new PreviewPaneViewModel(mockMessenger.Object);

        // Assert
        viewModel.SelectedClip.ShouldBeNull();
        viewModel.PreviewText.ShouldBeEmpty();
        viewModel.PreviewHtml.ShouldBeEmpty();
        viewModel.PreviewImageSource.ShouldBeNull();
        viewModel.HasTextPreview.ShouldBeFalse();
        viewModel.HasHtmlPreview.ShouldBeFalse();
        viewModel.HasImagePreview.ShouldBeFalse();
    }

    [Fact]
    public void Receive_WithTextClip_ShouldSetPreviewText()
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
        viewModel.SelectedClip.ShouldBe(clip);
        viewModel.PreviewText.ShouldBe("Test content");
        viewModel.HasTextPreview.ShouldBeTrue();
        viewModel.HasHtmlPreview.ShouldBeFalse();
        viewModel.HasImagePreview.ShouldBeFalse();
    }

    [Fact]
    public void Receive_WithHtmlClip_ShouldSetPreviewHtml()
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
        viewModel.SelectedClip.ShouldBe(clip);
        viewModel.PreviewHtml.ShouldBe("<h1>Test</h1>");
        viewModel.HasHtmlPreview.ShouldBeTrue();
        viewModel.HasTextPreview.ShouldBeFalse();
        viewModel.HasImagePreview.ShouldBeFalse();
    }

    [Fact]
    public void Receive_WithRichTextClip_ShouldSetPreviewText()
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
        viewModel.SelectedClip.ShouldBe(clip);
        viewModel.PreviewText.ShouldBe("Test RTF");
        viewModel.HasTextPreview.ShouldBeTrue();
    }

    [Fact]
    public void Receive_WithImageClip_ShouldSetImagePreview()
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
        viewModel.SelectedClip.ShouldBe(clip);
        viewModel.HasImagePreview.ShouldBeTrue();
        viewModel.HasTextPreview.ShouldBeFalse();
        viewModel.HasHtmlPreview.ShouldBeFalse();
        // Note: PreviewImageSource may be null if image data is invalid, which is OK for this test
    }

    [Fact]
    public void Receive_WithNull_ShouldClearPreview()
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
        viewModel.SelectedClip.ShouldBeNull();
        viewModel.PreviewText.ShouldBeEmpty();
        viewModel.PreviewHtml.ShouldBeEmpty();
        viewModel.PreviewImageSource.ShouldBeNull();
        viewModel.HasTextPreview.ShouldBeFalse();
        viewModel.HasHtmlPreview.ShouldBeFalse();
        viewModel.HasImagePreview.ShouldBeFalse();
    }

    [Fact]
    public void SelectedClip_WhenSet_ShouldRaisePropertyChanged()
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
        propertyChangedRaised.ShouldBeTrue();
    }

    [Fact]
    public void Receive_WithNullClip_ShouldClearAllPreviewData()
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
        viewModel.SelectedClip.ShouldBeNull();
        viewModel.PreviewText.ShouldBeEmpty();
        viewModel.PreviewHtml.ShouldBeEmpty();
        viewModel.PreviewImageSource.ShouldBeNull();
        viewModel.HasTextPreview.ShouldBeFalse();
        viewModel.HasHtmlPreview.ShouldBeFalse();
        viewModel.HasImagePreview.ShouldBeFalse();
    }
}
