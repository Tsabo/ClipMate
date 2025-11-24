using ClipMate.Core.Models;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using Windows.Win32.Foundation;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for PasteService covering paste operations and Win32 API integration.
/// NOTE: Tests focus on service logic and parameter validation since unsafe pointer methods
/// cannot be easily mocked with Moq. Win32 API behavior is tested via integration tests.
/// </summary>
public class PasteServiceTests : TestFixtureBase
{
    private PasteService CreateService()
    {
        var mockInterop = CreateWin32InputMock();
        
        // Setup default successful responses
        mockInterop.Setup(w => w.GetForegroundWindow()).Returns(new HWND(new IntPtr(12345)));
        mockInterop.Setup(w => w.GetWindowThreadProcessId(It.IsAny<HWND>(), out It.Ref<uint>.IsAny))
            .Returns((HWND hwnd, out uint processId) =>
            {
                processId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                return 1u;
            });
        
        return new PasteService(mockInterop.Object);
    }

    #region Constructor Tests

    [Test]
    public async Task Constructor_WithNullInterop_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new PasteService(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidInterop_ShouldCreateInstance()
    {
        // Arrange
        var mockInterop = CreateWin32InputMock();

        // Act
        var service = new PasteService(mockInterop.Object);

        // Assert
        await Assert.That(service).IsNotNull();
    }

    #endregion

    #region PasteToActiveWindowAsync Tests

    [Test]
    public async Task PasteToActiveWindowAsync_WithNullClip_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.PasteToActiveWindowAsync(null!);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PasteToActiveWindowAsync_WithEmptyTextContent_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = string.Empty,
            CapturedAt = DateTime.UtcNow
        };

        // Act
        var result = await service.PasteToActiveWindowAsync(clip);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PasteToActiveWindowAsync_WithNullTextContent_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = null,
            CapturedAt = DateTime.UtcNow
        };

        // Act
        var result = await service.PasteToActiveWindowAsync(clip);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PasteToActiveWindowAsync_WithImageClip_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Image,
            CapturedAt = DateTime.UtcNow
        };

        // Act
        var result = await service.PasteToActiveWindowAsync(clip);

        // Assert - Image paste not yet implemented
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PasteToActiveWindowAsync_WithRichTextClip_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.RichText,
            CapturedAt = DateTime.UtcNow
        };

        // Act
        var result = await service.PasteToActiveWindowAsync(clip);

        // Assert - RichText paste not yet implemented
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region PasteTextAsync Tests

    [Test]
    public async Task PasteTextAsync_WithNullText_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.PasteTextAsync(null!);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PasteTextAsync_WithEmptyText_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.PasteTextAsync(string.Empty);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PasteTextAsync_WithWhitespaceOnly_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.PasteTextAsync("   ");

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region GetActiveWindowTitle Tests

    [Test]
    public async Task GetActiveWindowTitle_WithNoActiveWindow_ShouldReturnEmpty()
    {
        // Arrange
        var mockInterop = CreateWin32InputMock();
        mockInterop.Setup(w => w.GetForegroundWindow()).Returns(new HWND(IntPtr.Zero));
        
        var service = new PasteService(mockInterop.Object);

        // Act
        var title = service.GetActiveWindowTitle();

        // Assert
        await Assert.That(title).IsEmpty();
    }

    #endregion

    #region GetActiveWindowProcessName Tests

    [Test]
    public async Task GetActiveWindowProcessName_WithNoActiveWindow_ShouldReturnEmpty()
    {
        // Arrange
        var mockInterop = CreateWin32InputMock();
        mockInterop.Setup(w => w.GetForegroundWindow()).Returns(new HWND(IntPtr.Zero));
        
        var service = new PasteService(mockInterop.Object);

        // Act
        var processName = service.GetActiveWindowProcessName();

        // Assert
        await Assert.That(processName).IsEmpty();
    }

    [Test]
    public async Task GetActiveWindowProcessName_WhenGetWindowThreadProcessIdFails_ShouldReturnEmpty()
    {
        // Arrange
        var mockInterop = CreateWin32InputMock();
        mockInterop.Setup(w => w.GetForegroundWindow()).Returns(new HWND(new IntPtr(12345)));
        mockInterop.Setup(w => w.GetWindowThreadProcessId(It.IsAny<HWND>(), out It.Ref<uint>.IsAny))
            .Returns((HWND hwnd, out uint processId) =>
            {
                processId = 0; // Failed to get process ID
                return 0u;
            });
        
        var service = new PasteService(mockInterop.Object);

        // Act
        var processName = service.GetActiveWindowProcessName();

        // Assert
        await Assert.That(processName).IsEmpty();
    }

    #endregion
}
