using ClipMate.Platform.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class QuickPasteServiceTests
{
    [Test]
    public async Task Constructor_ThrowsArgumentNullException_WhenWin32InteropIsNull()
    {
        // Arrange
        var mockClipboard = CreateMockClipboardService();
        var mockConfig = CreateMockConfigurationService();
        var mockMessenger = CreateMockMessenger();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                null!,
                mockClipboard,
                mockConfig,
                mockMessenger,
                CreateMockMacroExecutionService(),
                CreateMockTemplateService(),
                CreateMockDialogService(),
                mockLogger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_ThrowsArgumentNullException_WhenClipboardServiceIsNull()
    {
        // Arrange
        var mockWin32 = CreateMockWin32();
        var mockConfig = CreateMockConfigurationService();
        var mockMessenger = CreateMockMessenger();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                mockWin32,
                null!,
                mockConfig,
                mockMessenger,
                CreateMockMacroExecutionService(),
                CreateMockTemplateService(),
                CreateMockDialogService(),
                mockLogger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_ThrowsArgumentNullException_WhenConfigurationServiceIsNull()
    {
        // Arrange
        var mockWin32 = CreateMockWin32();
        var mockClipboard = CreateMockClipboardService();
        var mockMessenger = CreateMockMessenger();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                mockWin32,
                mockClipboard,
                null!,
                mockMessenger,
                CreateMockMacroExecutionService(),
                CreateMockTemplateService(),
                CreateMockDialogService(),
                mockLogger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_ThrowsArgumentNullException_WhenMessengerIsNull()
    {
        // Arrange
        var mockWin32 = CreateMockWin32();
        var mockClipboard = CreateMockClipboardService();
        var mockConfig = CreateMockConfigurationService();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                mockWin32,
                mockClipboard,
                mockConfig,
                null!,
                CreateMockMacroExecutionService(),
                CreateMockTemplateService(),
                CreateMockDialogService(),
                mockLogger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var mockWin32 = CreateMockWin32();
        var mockClipboard = CreateMockClipboardService();
        var mockConfig = CreateMockConfigurationService();
        var mockMessenger = CreateMockMessenger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                mockWin32,
                mockClipboard,
                mockConfig,
                mockMessenger,
                CreateMockMacroExecutionService(),
                CreateMockTemplateService(),
                CreateMockDialogService(),
                null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_SelectsDefaultFormattingString()
    {
        // Act
        var service = CreateService();

        // Assert
        var selected = service.GetSelectedFormattingString();
        await Assert.That(selected).IsNotNull();
        await Assert.That(selected!.Title).IsEqualTo("Default - Ctrl+V");
        await Assert.That(selected.TitleTrigger).IsEqualTo("*");
    }
}
