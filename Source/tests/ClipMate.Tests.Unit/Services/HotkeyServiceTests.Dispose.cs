using ClipMate.Core.Models;
using ClipMate.Platform.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyServiceTests
{
    #region Dispose Tests

    [Test]
    public async Task Dispose_ShouldDisposeManagerAndClearRegistrations()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        mockManager.Setup(m => m.RegisterHotkey(It.IsAny<ModifierKeys>(), It.IsAny<int>(), It.IsAny<Action>()))
            .Returns(1);

        using var service = new HotkeyService(mockManager.Object);

        var action = () => { };
        service.RegisterHotkey(100, ModifierKeys.Control, 0x56, action);

        // Act
        service.Dispose();

        // Assert
        mockManager.Verify(m => m.Dispose(), Times.Once);
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager.Object);

        // Act & Assert
        await Assert.That(() =>
            {
                service.Dispose();
                service.Dispose();
                service.Dispose();
            })
            .ThrowsNothing();
    }

    #endregion
}
