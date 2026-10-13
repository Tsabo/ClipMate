using ClipMate.Core.Models;
using ClipMate.Platform.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyServiceTests
{
    [Test]
    public async Task Dispose_ShouldDisposeManagerAndClearRegistrations()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        A.CallTo(() => mockManager.RegisterHotkey(A<ModifierKeys>.Ignored, A<int>.Ignored, A<Action>.Ignored))
            .Returns(1);

        using var service = new HotkeyService(mockManager);

        var action = () => { };
        service.RegisterHotkey(100, ModifierKeys.Control, 0x56, action);

        // Act
        service.Dispose();

        // Assert
        A.CallTo(() => mockManager.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager);

        // Act & Assert
        await Assert.That(() =>
            {
                service.Dispose();
                service.Dispose();
                service.Dispose();
            })
            .ThrowsNothing();
    }
}
