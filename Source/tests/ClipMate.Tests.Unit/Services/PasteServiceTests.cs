using System.Diagnostics;
using Windows.Win32.Foundation;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for PasteService covering paste operations and Win32 API integration.
/// NOTE: Tests focus on service logic and parameter validation since unsafe pointer methods
/// cannot be easily mocked with FakeItEasy. Win32 API behavior is tested via integration tests.
/// </summary>
[Category("PasteService")]
[Category("Unit")]
public class PasteServiceTests : TestFixtureBase
{
    private PasteService CreateService()
    {
        var mockInterop = CreateWin32InputMock();
        var mockClipboardService = A.Fake<IClipboardService>();
        var mockLogger = A.Fake<ILogger<PasteService>>();

        // Setup default successful responses
        A.CallTo(() => mockInterop.GetForegroundWindow()).Returns(new HWND(new IntPtr(12345)));
        uint dummyProcessId = default;
        A.CallTo(() => mockInterop.GetWindowThreadProcessId(A<HWND>.Ignored, out dummyProcessId))
            .Returns(1u)
            .AssignsOutAndRefParametersLazily(call => new object[] { (uint)Process.GetCurrentProcess().Id });

        return new PasteService(mockInterop, mockClipboardService, mockLogger);
    }

    [Category("GetActiveWindowTitle")]
    public class GetActiveWindowTitleTests : PasteServiceTests
    {
        [Test]
        public async Task WithNoActiveWindow_ShouldReturnEmpty()
        {
            // Arrange
            var mockInterop = CreateWin32InputMock();
            var mockClipboardService = A.Fake<IClipboardService>();
            var mockLogger = A.Fake<ILogger<PasteService>>();
            A.CallTo(() => mockInterop.GetForegroundWindow()).Returns(new HWND(IntPtr.Zero));

            var service = new PasteService(mockInterop, mockClipboardService, mockLogger);

            // Act
            var title = service.GetActiveWindowTitle();

            // Assert
            await Assert.That(title).IsEmpty();
        }
    }

    [Category("Constructor")]
    public class ConstructorTests : PasteServiceTests
    {
        [Test]
        public async Task WithNullInterop_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockClipboardService = A.Fake<IClipboardService>();
            var mockLogger = A.Fake<ILogger<PasteService>>();

            // Act & Assert
            await Assert.That(() => new PasteService(null!, mockClipboardService, mockLogger))
                .Throws<ArgumentNullException>();
        }

        [Test]
        public async Task WithNullClipboardService_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockInterop = CreateWin32InputMock();
            var mockLogger = A.Fake<ILogger<PasteService>>();

            // Act & Assert
            await Assert.That(() => new PasteService(mockInterop, null!, mockLogger))
                .Throws<ArgumentNullException>();
        }

        [Test]
        public async Task WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockInterop = CreateWin32InputMock();
            var mockClipboardService = A.Fake<IClipboardService>();

            // Act & Assert
            await Assert.That(() => new PasteService(mockInterop, mockClipboardService, null!))
                .Throws<ArgumentNullException>();
        }

        [Test]
        public async Task WithValidDependencies_ShouldCreateInstance()
        {
            // Arrange
            var mockInterop = CreateWin32InputMock();
            var mockClipboardService = A.Fake<IClipboardService>();
            var mockLogger = A.Fake<ILogger<PasteService>>();

            // Act
            var service = new PasteService(mockInterop, mockClipboardService, mockLogger);

            // Assert
            await Assert.That(service).IsNotNull();
        }
    }

    [Category("PasteToActiveWindow")]
    public class PasteToActiveWindowAsyncTests : PasteServiceTests
    {
        [Test]
        public async Task WithNullClip_ShouldReturnFalse()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.PasteToActiveWindowAsync(null!);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task WithEmptyTextContent_ShouldReturnFalse()
        {
            // Arrange
            var service = CreateService();
            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Text,
                TextContent = string.Empty,
                CapturedAt = DateTime.UtcNow,
            };

            // Act
            var result = await service.PasteToActiveWindowAsync(clip);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task WithNullTextContent_ShouldReturnFalse()
        {
            // Arrange
            var service = CreateService();
            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Text,
                TextContent = null,
                CapturedAt = DateTime.UtcNow,
            };

            // Act
            var result = await service.PasteToActiveWindowAsync(clip);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task WithImageClip_ShouldReturnFalse()
        {
            // Arrange
            var service = CreateService();
            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Image,
                CapturedAt = DateTime.UtcNow,
            };

            // Act
            var result = await service.PasteToActiveWindowAsync(clip);

            // Assert - Image paste not yet implemented
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task WithRichTextClip_ShouldReturnFalse()
        {
            // Arrange
            var service = CreateService();
            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.RichText,
                CapturedAt = DateTime.UtcNow,
            };

            // Act
            var result = await service.PasteToActiveWindowAsync(clip);

            // Assert - RichText paste not yet implemented
            await Assert.That(result).IsFalse();
        }
    }

    [Category("GetActiveWindowProcessName")]
    public class GetActiveWindowProcessNameTests : PasteServiceTests
    {
        [Test]
        public async Task WithNoActiveWindow_ShouldReturnEmpty()
        {
            // Arrange
            var mockInterop = CreateWin32InputMock();
            var mockClipboardService = A.Fake<IClipboardService>();
            var mockLogger = A.Fake<ILogger<PasteService>>();
            A.CallTo(() => mockInterop.GetForegroundWindow()).Returns(new HWND(IntPtr.Zero));

            var service = new PasteService(mockInterop, mockClipboardService, mockLogger);

            // Act
            var processName = service.GetActiveWindowProcessName();

            // Assert
            await Assert.That(processName).IsEmpty();
        }

        [Test]
        public async Task WhenGetWindowThreadProcessIdFails_ShouldReturnEmpty()
        {
            // Arrange
            var mockInterop = CreateWin32InputMock();
            var mockClipboardService = A.Fake<IClipboardService>();
            var mockLogger = A.Fake<ILogger<PasteService>>();
            A.CallTo(() => mockInterop.GetForegroundWindow()).Returns(new HWND(new IntPtr(12345)));
            uint dummyProcessId = default;
            A.CallTo(() => mockInterop.GetWindowThreadProcessId(A<HWND>.Ignored, out dummyProcessId))
                .Returns(0u)
                .AssignsOutAndRefParametersLazily(call => new object[] { 0u }); // Failed to get process ID

            var service = new PasteService(mockInterop, mockClipboardService, mockLogger);

            // Act
            var processName = service.GetActiveWindowProcessName();

            // Assert
            await Assert.That(processName).IsEmpty();
        }
    }
}
