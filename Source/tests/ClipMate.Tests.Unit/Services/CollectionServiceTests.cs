using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class CollectionServiceTests
{
    private readonly Mock<ICollectionRepository> _mockRepository;

    public CollectionServiceTests()
    {
        _mockRepository = new Mock<ICollectionRepository>();
    }

    private ICollectionService CreateCollectionService()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _mockRepository.Object);
        var serviceProvider = services.BuildServiceProvider();
        return new CollectionService(serviceProvider);
    }

    private Collection CreateTestCollection(Guid? id = null, string name = "Test Collection")
    {
        return new Collection
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    [Test]
    public async Task CreateAsync_WithValidName_ShouldCreateAndReturnCollection()
    {
        // Arrange
        var name = "Test Collection";
        var description = "Test Description";
        
        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection c, CancellationToken _) => c);

        var service = CreateCollectionService();

        // Act
        var result = await service.CreateAsync(name, description);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo(name);
        await Assert.That(result.Description).IsEqualTo(description);
        await Assert.That(result.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(result.CreatedAt).IsGreaterThan(DateTime.UtcNow.AddSeconds(-5));
        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateAsync_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.CreateAsync(null!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task CreateAsync_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.CreateAsync("")).Throws<ArgumentException>();
    }

    [Test]
    public async Task GetByIdAsync_WithValidId_ShouldReturnCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var expectedCollection = CreateTestCollection(collectionId);
        _mockRepository.Setup(r => r.GetByIdAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCollection);

        var service = CreateCollectionService();

        // Act
        var result = await service.GetByIdAsync(collectionId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(collectionId);
    }

    [Test]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByIdAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var service = CreateCollectionService();

        // Act
        var result = await service.GetByIdAsync(collectionId);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnAllCollections()
    {
        // Arrange
        var collections = new List<Collection>
        {
            CreateTestCollection(name: "Collection 1"),
            CreateTestCollection(name: "Collection 2"),
            CreateTestCollection(name: "Collection 3")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(collections);

        var service = CreateCollectionService();

        // Act
        var result = await service.GetAllAsync();

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].Name).IsEqualTo("Collection 1");
        await Assert.That(result[1].Name).IsEqualTo("Collection 2");
        await Assert.That(result[2].Name).IsEqualTo("Collection 3");
    }

    [Test]
    public async Task UpdateAsync_WithValidCollection_ShouldUpdate()
    {
        // Arrange
        var collection = CreateTestCollection();
        collection.Name = "Updated Name";

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateCollectionService();

        // Act
        await service.UpdateAsync(collection);

        // Assert
        await Assert.That(collection.Name).IsEqualTo("Updated Name");
        await Assert.That(collection.ModifiedAt).IsNotNull();
        _mockRepository.Verify(r => r.UpdateAsync(collection, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.UpdateAsync(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task UpdateAsync_WhenRepositoryFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var collection = CreateTestCollection();
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.UpdateAsync(collection)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DeleteAsync_WithValidId_ShouldDeleteCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        _mockRepository.Setup(r => r.DeleteAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateCollectionService();

        // Act
        await service.DeleteAsync(collectionId);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(collectionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_WhenRepositoryFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        _mockRepository.Setup(r => r.DeleteAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.DeleteAsync(collectionId)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetActiveAsync_WhenNoActiveSet_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.GetActiveAsync()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetActiveAsync_WithActiveSet_ShouldReturnActiveCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var collection = CreateTestCollection(collectionId);
        
        _mockRepository.Setup(r => r.GetByIdAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        var service = CreateCollectionService();
        await service.SetActiveAsync(collectionId);

        // Act
        var result = await service.GetActiveAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsEqualTo(collectionId);
    }

    [Test]
    public async Task SetActiveAsync_WithValidId_ShouldSetActive()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var collection = CreateTestCollection(collectionId);
        
        _mockRepository.Setup(r => r.GetByIdAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        var service = CreateCollectionService();

        // Act
        await service.SetActiveAsync(collectionId);

        // Assert - GetActiveAsync should return the collection
        var active = await service.GetActiveAsync();
        await Assert.That(active.Id).IsEqualTo(collectionId);
    }

    [Test]
    public async Task SetActiveAsync_WithInvalidId_ShouldThrowArgumentException()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByIdAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.SetActiveAsync(collectionId)).Throws<ArgumentException>();
    }

    [Test]
    public async Task DeleteAsync_WithActiveCollection_ShouldClearActive()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var collection = CreateTestCollection(collectionId);
        
        _mockRepository.Setup(r => r.GetByIdAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);
        _mockRepository.Setup(r => r.DeleteAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateCollectionService();
        await service.SetActiveAsync(collectionId);

        // Act
        await service.DeleteAsync(collectionId);

        // Assert - GetActiveAsync should throw since active was cleared
        await Assert.That(async () => await service.GetActiveAsync()).Throws<InvalidOperationException>();
    }
}
