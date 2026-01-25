using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class ApplicationFilterServiceTests
{
    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullContextFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ApplicationFilterService(
            null!,
            _mockCollectionService.Object,
            _mockSoundService.Object,
            _mockLogger.Object));

        await Assert.That(exception.ParamName).IsEqualTo("contextFactory");
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullCollectionService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ApplicationFilterService(
            _mockContextFactory.Object,
            null!,
            _mockSoundService.Object,
            _mockLogger.Object));

        await Assert.That(exception.ParamName).IsEqualTo("collectionService");
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullSoundService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ApplicationFilterService(
            _mockContextFactory.Object,
            _mockCollectionService.Object,
            null!,
            _mockLogger.Object));

        await Assert.That(exception.ParamName).IsEqualTo("soundService");
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ApplicationFilterService(
            _mockContextFactory.Object,
            _mockCollectionService.Object,
            _mockSoundService.Object,
            null!));

        await Assert.That(exception.ParamName).IsEqualTo("logger");
    }
}
