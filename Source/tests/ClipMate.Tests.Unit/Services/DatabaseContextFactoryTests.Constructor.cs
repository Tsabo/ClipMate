using ClipMate.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Constructor validation tests for DatabaseContextFactory.
/// </summary>
public partial class DatabaseContextFactoryTests
{
    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var configService = CreateConfigService();
        var logger = A.Fake<ILogger<DatabaseContextFactory>>();

        // Act & Assert
        await Assert.That(() => new DatabaseContextFactory(null!, configService, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullConfigurationService_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var logger = A.Fake<ILogger<DatabaseContextFactory>>();

        // Act & Assert
        await Assert.That(() => new DatabaseContextFactory(serviceProvider, null!, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var configService = CreateConfigService();

        // Act & Assert
        await Assert.That(() => new DatabaseContextFactory(serviceProvider, configService, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithValidParameters_Succeeds()
    {
        // Arrange & Act
        var factory = CreateFactory();

        // Assert
        await Assert.That(factory).IsNotNull();
        await Assert.That(factory.GetLoadedDatabasePaths()).IsEmpty();
    }
}
