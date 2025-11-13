using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for IClipboardService implementation.
/// Tests written first per TDD requirement for User Story 1.
/// </summary>
public class ClipboardServiceTests : TestFixtureBase
{
    [StaFact]
    public async Task StartMonitoringAsync_ShouldSetIsMonitoringToTrue()
    {
        // Arrange
        var service = CreateClipboardService();

        // Act
        await service.StartMonitoringAsync();

        // Assert
        service.IsMonitoring.ShouldBeTrue();
    }

    [StaFact]
    public async Task StopMonitoringAsync_ShouldSetIsMonitoringToFalse()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act
        await service.StopMonitoringAsync();

        // Assert
        service.IsMonitoring.ShouldBeFalse();
    }

    [StaFact]
    public async Task StartMonitoringAsync_WhenAlreadyMonitoring_ShouldNotThrow()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act & Assert
        await Should.NotThrowAsync(async () => await service.StartMonitoringAsync());
    }

    [StaFact]
    public async Task GetCurrentClipboardContentAsync_WithTextContent_ShouldReturnClipWithTextType()
    {
        // Arrange
        var service = CreateClipboardService();
        // TODO: Need to mock clipboard content

        // Act
        var clip = await service.GetCurrentClipboardContentAsync();

        // Assert
        clip.ShouldNotBeNull();
        clip.Type.ShouldBe(ClipType.Text);
        clip.TextContent.ShouldNotBeNullOrEmpty();
    }

    [StaFact]
    public async Task GetCurrentClipboardContentAsync_WithImageContent_ShouldReturnClipWithImageType()
    {
        // Arrange
        var service = CreateClipboardService();
        // TODO: Need to mock clipboard with image data

        // Act
        var clip = await service.GetCurrentClipboardContentAsync();

        // Assert
        clip.ShouldNotBeNull();
        clip.Type.ShouldBe(ClipType.Image);
        clip.ImageData.ShouldNotBeNull();
    }

    [StaFact]
    public async Task GetCurrentClipboardContentAsync_WithEmptyClipboard_ShouldReturnNull()
    {
        // Arrange
        var service = CreateClipboardService();
        // TODO: Need to mock empty clipboard

        // Act
        var clip = await service.GetCurrentClipboardContentAsync();

        // Assert
        clip.ShouldBeNull();
    }

    [StaFact]
    public async Task SetClipboardContentAsync_WithTextClip_ShouldSetClipboardText()
    {
        // Arrange
        var service = CreateClipboardService();
        var clip = new Clip
        {
            Type = ClipType.Text,
            TextContent = "Test clipboard content"
        };

        // Act
        await service.SetClipboardContentAsync(clip);

        // Assert
        // TODO: Verify clipboard contains the text
    }

    [StaFact]
    public void ClipCaptured_WhenClipboardChanges_ShouldRaiseEvent()
    {
        // Arrange
        var service = CreateClipboardService();
        Clip? capturedClip = null;
        service.ClipCaptured += (sender, args) =>
        {
            capturedClip = args.Clip;
        };

        // Act
        // TODO: Simulate clipboard change

        // Assert
        capturedClip.ShouldNotBeNull();
    }

    [StaFact]
    public async Task ClipCaptured_WithDuplicateContent_ShouldCalculateCorrectContentHash()
    {
        // Arrange
        var service = CreateClipboardService();
        var clips = new List<Clip>();
        service.ClipCaptured += (sender, args) =>
        {
            clips.Add(args.Clip);
        };

        // Act
        // TODO: Simulate clipboard change with same content twice

        // Assert
        clips.Count.ShouldBeGreaterThanOrEqualTo(2);
        clips[0].ContentHash.ShouldBe(clips[1].ContentHash);
    }

    [StaFact]
    public async Task ClipCaptured_EventHandler_CanCancelSave()
    {
        // Arrange
        var service = CreateClipboardService();
        service.ClipCaptured += (sender, args) =>
        {
            args.Cancel = true; // Handler can cancel save
        };

        // Act
        // TODO: Simulate clipboard change

        // Assert
        // Verify that save was cancelled
    }

    /// <summary>
    /// Creates an instance of ClipboardService for testing.
    /// </summary>
    private IClipboardService CreateClipboardService()
    {
        var logger = CreateLogger<ClipboardService>();
        return new ClipboardService(logger);
    }
}
