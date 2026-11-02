using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteTests
{
    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        await Assert.That(service).IsNotNull();
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(service.CurrentPosition).IsEqualTo(-1);
        await Assert.That(service.TotalCount).IsEqualTo(0);
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullClipboard_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new PowerPaste(null!, _mockClipboardService,
            _mockCurrentClipWriter, _mockTemplateService, _mockConfigService, _mockSoundService, _mockLogger));

        await Assert.That(exception.ParamName).IsEqualTo("clipboard");
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullConfigService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new PowerPaste(_clipboard, _mockClipboardService,
            _mockCurrentClipWriter, _mockTemplateService, null!, _mockSoundService, _mockLogger));

        await Assert.That(exception.ParamName).IsEqualTo("configurationService");
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new PowerPaste(_clipboard, _mockClipboardService,
            _mockCurrentClipWriter, _mockTemplateService, _mockConfigService, _mockSoundService, null!));

        await Assert.That(exception.ParamName).IsEqualTo("logger");
    }
}
