using ClipMate.Core.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    [Test]
    public async Task Constructor_ShouldCreateInstance()
    {
        // Arrange & Act
        var service = new TextTransformService();

        // Assert
        await Assert.That(service).IsNotNull();
    }
}
