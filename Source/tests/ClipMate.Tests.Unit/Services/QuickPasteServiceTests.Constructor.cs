using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class QuickPasteServiceTests
{
    [Test]
    public async Task Constructor_ThrowsArgumentNullException_WhenWin32InteropIsNull()
    {
        // Arrange
        var mockCurrentClipWriter = CreateMockCurrentClipWriter();
        var mockConfig = CreateMockConfigurationService();
        var mockMessenger = CreateMockMessenger();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                null!,
                CreateMockPasteTarget(),
                mockCurrentClipWriter,
                A.Fake<IDatabases>(),
                mockConfig,
                mockMessenger,
                CreateMockMacroExecutionService(),
                CreateMockTemplateService(),
                CreateMockDialogService(),
                mockLogger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_ThrowsArgumentNullException_WhenCurrentClipWriterIsNull()
    {
        // Arrange
        var mockWin32 = CreateMockWin32();
        var mockConfig = CreateMockConfigurationService();
        var mockMessenger = CreateMockMessenger();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                mockWin32,
                CreateMockPasteTarget(),
                null!,
                A.Fake<IDatabases>(),
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
        var mockCurrentClipWriter = CreateMockCurrentClipWriter();
        var mockMessenger = CreateMockMessenger();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                mockWin32,
                CreateMockPasteTarget(),
                mockCurrentClipWriter,
                A.Fake<IDatabases>(),
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
        var mockCurrentClipWriter = CreateMockCurrentClipWriter();
        var mockConfig = CreateMockConfigurationService();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                mockWin32,
                CreateMockPasteTarget(),
                mockCurrentClipWriter,
                A.Fake<IDatabases>(),
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
        var mockCurrentClipWriter = CreateMockCurrentClipWriter();
        var mockConfig = CreateMockConfigurationService();
        var mockMessenger = CreateMockMessenger();

        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                mockWin32,
                CreateMockPasteTarget(),
                mockCurrentClipWriter,
                A.Fake<IDatabases>(),
                mockConfig,
                mockMessenger,
                CreateMockMacroExecutionService(),
                CreateMockTemplateService(),
                CreateMockDialogService(),
                null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_ThrowsArgumentNullException_WhenPasteTargetIsNull()
    {
        // Act & Assert
        await Assert.That(() => new QuickPasteService(
                CreateMockWin32(),
                null!,
                CreateMockCurrentClipWriter(),
                A.Fake<IDatabases>(),
                CreateMockConfigurationService(),
                CreateMockMessenger(),
                CreateMockMacroExecutionService(),
                CreateMockTemplateService(),
                CreateMockDialogService(),
                CreateMockLogger()))
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
