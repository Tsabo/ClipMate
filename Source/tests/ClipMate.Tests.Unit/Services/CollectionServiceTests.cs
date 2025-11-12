using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Moq;
using Shouldly;
using Xunit;

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
        return new CollectionService(_mockRepository.Object);
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

    [Fact]
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
        result.ShouldNotBeNull();
        result.Name.ShouldBe(name);
        result.Description.ShouldBe(description);
        result.Id.ShouldNotBe(Guid.Empty);
        result.CreatedAt.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () => await service.CreateAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () => await service.CreateAsync(""));
    }

    [Fact]
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
        result.ShouldNotBeNull();
        result!.Id.ShouldBe(collectionId);
    }

    [Fact]
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
        result.ShouldBeNull();
    }

    [Fact]
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
        result.Count.ShouldBe(3);
        result[0].Name.ShouldBe("Collection 1");
        result[1].Name.ShouldBe("Collection 2");
        result[2].Name.ShouldBe("Collection 3");
    }

    [Fact]
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
        collection.Name.ShouldBe("Updated Name");
        collection.ModifiedAt.ShouldNotBeNull();
        _mockRepository.Verify(r => r.UpdateAsync(collection, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () => await service.UpdateAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_WhenRepositoryFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var collection = CreateTestCollection();
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateCollectionService();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () => await service.UpdateAsync(collection));
    }

    [Fact]
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

    [Fact]
    public async Task DeleteAsync_WhenRepositoryFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        _mockRepository.Setup(r => r.DeleteAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateCollectionService();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () => await service.DeleteAsync(collectionId));
    }

    [Fact]
    public async Task GetActiveAsync_WhenNoActiveSet_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () => await service.GetActiveAsync());
    }

    [Fact]
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
        result.ShouldNotBeNull();
        result.Id.ShouldBe(collectionId);
    }

    [Fact]
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
        active.Id.ShouldBe(collectionId);
    }

    [Fact]
    public async Task SetActiveAsync_WithInvalidId_ShouldThrowArgumentException()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByIdAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var service = CreateCollectionService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () => await service.SetActiveAsync(collectionId));
    }

    [Fact]
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
        await Should.ThrowAsync<InvalidOperationException>(async () => await service.GetActiveAsync());
    }
}
