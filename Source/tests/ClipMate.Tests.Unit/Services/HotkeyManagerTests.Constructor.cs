using ClipMate.Platform;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyManagerTests
{
    #region Constructor Tests

    [Test]
    public async Task Constructor_WithNullInterop_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new HotkeyManager(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidInterop_ShouldCreateInstance()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();

        // Act
        var manager = new HotkeyManager(mockInterop.Object);

        // Assert
        await Assert.That(manager).IsNotNull();
    }

    #endregion
}
