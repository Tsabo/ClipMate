using ClipMate.Core.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    #region Constructor Tests

    [Test]
    public async Task Constructor_ShouldCreateInstance()
    {
        // Arrange & Act
        var service = new TextTransformService();

        // Assert
        await Assert.That(service).IsNotNull();
    }

    #endregion
}
