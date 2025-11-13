using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for SystemTrayService.
/// Note: Some methods are difficult to test due to Windows Forms dependencies (NotifyIcon).
/// These tests focus on testable logic and event handling.
/// </summary>
public class SystemTrayServiceTests
{
    private readonly Mock<ICollectionService> _mockCollectionService;
    private readonly Mock<ILogger<SystemTrayService>> _mockLogger;

    public SystemTrayServiceTests()
    {
        _mockCollectionService = new Mock<ICollectionService>();
        _mockLogger = new Mock<ILogger<SystemTrayService>>();
    }

    [Fact]
    public void Initialize_ShouldNotThrow()
    {
        // Arrange
        var service = CreateSystemTrayService();

        // Act & Assert
        Should.NotThrow(() => service.Initialize());
    }

    [Fact]
    public void Initialize_CalledTwice_ShouldLogWarning()
    {
        // Arrange
        var service = CreateSystemTrayService();

        // Act
        service.Initialize();
        service.Initialize(); // Second call

        // Assert
        // Verify that a warning was logged on second initialization
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already initialized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ShowWindowRequested_Event_CanBeSubscribedTo()
    {
        // Arrange
        var service = CreateSystemTrayService();
        service.Initialize();
        
        var eventRaised = false;
        service.ShowWindowRequested += (sender, args) => eventRaised = true;

        // Act
        // Manually raise the event (simulating tray icon double-click)
        // Note: In real scenario, this would be triggered by NotifyIcon.DoubleClick
        
        // Assert
        // Just verify the event can be subscribed to without errors
        eventRaised.ShouldBeFalse(); // Not raised yet
    }

    [Fact]
    public void ExitRequested_Event_CanBeSubscribedTo()
    {
        // Arrange
        var service = CreateSystemTrayService();
        service.Initialize();
        
        var eventRaised = false;
        service.ExitRequested += (sender, args) => eventRaised = true;

        // Assert
        // Just verify the event can be subscribed to without errors
        eventRaised.ShouldBeFalse(); // Not raised yet
    }

    [Fact]
    public void CollectionChanged_Event_CanBeSubscribedTo()
    {
        // Arrange
        var service = CreateSystemTrayService();
        service.Initialize();
        
        Guid? changedCollectionId = null;
        service.CollectionChanged += (sender, collectionId) => changedCollectionId = collectionId;

        // Assert
        // Just verify the event can be subscribed to without errors
        changedCollectionId.ShouldBeNull(); // Not raised yet
    }

    [Fact]
    public void ShowBalloonNotification_WithTitleAndMessage_ShouldNotThrow()
    {
        // Arrange
        var service = CreateSystemTrayService();
        service.Initialize();

        // Act & Assert
        Should.NotThrow(() => service.ShowBalloonNotification("Test Title", "Test Message"));
    }

    [Fact]
    public void ShowBalloonNotification_WithFullParameters_ShouldNotThrow()
    {
        // Arrange
        var service = CreateSystemTrayService();
        service.Initialize();

        // Act & Assert
        Should.NotThrow(() => service.ShowBalloonNotification(
            "Test Title", 
            "Test Message", 
            System.Windows.Forms.ToolTipIcon.Info, 
            3000));
    }

    [Fact]
    public async Task RebuildContextMenuAsync_ShouldNotThrow()
    {
        // Arrange
        var service = CreateSystemTrayService();
        service.Initialize();

        _mockCollectionService.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Core.Models.Collection>());

        // Act & Assert
        await Should.NotThrowAsync(async () => await service.RebuildContextMenuAsync());
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var service = CreateSystemTrayService();
        service.Initialize();

        // Act & Assert
        Should.NotThrow(() => service.Dispose());
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        // Arrange
        var service = CreateSystemTrayService();
        service.Initialize();

        // Act & Assert
        Should.NotThrow(() =>
        {
            service.Dispose();
            service.Dispose(); // Second call should be safe
        });
    }

    [Fact]
    public void Dispose_WithoutInitialize_ShouldNotThrow()
    {
        // Arrange
        var service = CreateSystemTrayService();

        // Act & Assert (dispose without initialize)
        Should.NotThrow(() => service.Dispose());
    }

    private SystemTrayService CreateSystemTrayService()
    {
        return new SystemTrayService(_mockCollectionService.Object, _mockLogger.Object);
    }
}
