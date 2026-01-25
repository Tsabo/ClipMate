namespace ClipMate.Tests.Unit.Services;

public partial class DatabaseMaintenanceServiceTests
{
    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        await Assert.That(service).IsNotNull();
    }
}
