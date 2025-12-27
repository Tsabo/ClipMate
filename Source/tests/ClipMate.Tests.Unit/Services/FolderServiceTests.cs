using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class FolderServiceTests
{
    private readonly Mock<IDatabaseContextFactory> _mockContextFactory;
    private readonly Mock<ICollectionService> _mockCollectionService;
    private readonly Mock<IFolderRepository> _mockRepository;

    public FolderServiceTests()
    {
        _mockRepository = new Mock<IFolderRepository>();
        _mockContextFactory = new Mock<IDatabaseContextFactory>();
        _mockCollectionService = new Mock<ICollectionService>();
        
        _mockContextFactory.Setup(f => f.GetFolderRepository(It.IsAny<string>())).Returns(_mockRepository.Object);
        _mockCollectionService.Setup(c => c.GetActiveDatabaseKey()).Returns("test-db");
    }

    private IFolderService CreateFolderService() => new FolderService(_mockContextFactory.Object, _mockCollectionService.Object);

    private Folder CreateTestFolder(Guid? id = null, string name = "Test Folder", Guid? collectionId = null, Guid? parentFolderId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            CollectionId = collectionId ?? Guid.NewGuid(),
            ParentFolderId = parentFolderId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

    [Test]
    public async Task CreateAsync_WithValidParameters_ShouldCreateAndReturnFolder()
    {
        // Arrange
        const string name = "New Folder";
        var collectionId = Guid.NewGuid();

        _mockRepository.Setup(p => p.CreateAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);

        var service = CreateFolderService();

        // Act
        var result = await service.CreateAsync(name, collectionId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo(name);
        await Assert.That(result.CollectionId).IsEqualTo(collectionId);
        await Assert.That(result.ParentFolderId).IsNull();
        await Assert.That(result.Id).IsNotEqualTo(Guid.Empty);
        _mockRepository.Verify(p => p.CreateAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateAsync_WithParentFolder_ShouldSetParentFolderId()
    {
        // Arrange
        const string name = "Subfolder";
        var collectionId = Guid.NewGuid();
        var parentFolderId = Guid.NewGuid();

        _mockRepository.Setup(p => p.CreateAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);

        var service = CreateFolderService();

        // Act
        var result = await service.CreateAsync(name, collectionId, parentFolderId);

        // Assert
        await Assert.That(result.ParentFolderId).IsEqualTo(parentFolderId);
    }

    [Test]
    public async Task CreateAsync_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateFolderService();

        // Act & Assert
        await Assert.That(async () => await service.CreateAsync(null!, Guid.NewGuid())).Throws<ArgumentException>();
    }

    [Test]
    public async Task GetByIdAsync_WithValidId_ShouldReturnFolder()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        var expectedFolder = CreateTestFolder(folderId);
        _mockRepository.Setup(p => p.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFolder);

        var service = CreateFolderService();

        // Act
        var result = await service.GetByIdAsync(folderId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(folderId);
    }

    [Test]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        _mockRepository.Setup(p => p.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder?)null);

        var service = CreateFolderService();

        // Act
        var result = await service.GetByIdAsync(folderId);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByCollectionAsync_ShouldReturnAllFoldersInCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var folders = new List<Folder>
        {
            CreateTestFolder(name: "Folder 1", collectionId: collectionId),
            CreateTestFolder(name: "Folder 2", collectionId: collectionId),
            CreateTestFolder(name: "Folder 3", collectionId: collectionId),
        };

        _mockRepository.Setup(p => p.GetByCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folders);

        var service = CreateFolderService();

        // Act
        var result = await service.GetByCollectionAsync(collectionId);

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.All(f => f.CollectionId == collectionId)).IsTrue();
    }

    [Test]
    public async Task GetRootFoldersAsync_ShouldReturnOnlyRootFolders()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var rootFolders = new List<Folder>
        {
            CreateTestFolder(name: "Root 1", collectionId: collectionId, parentFolderId: null),
            CreateTestFolder(name: "Root 2", collectionId: collectionId, parentFolderId: null),
        };

        _mockRepository.Setup(p => p.GetRootFoldersAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rootFolders);

        var service = CreateFolderService();

        // Act
        var result = await service.GetRootFoldersAsync(collectionId);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(p => p.ParentFolderId == null)).IsTrue();
    }

    [Test]
    public async Task GetChildFoldersAsync_ShouldReturnChildFolders()
    {
        // Arrange
        var parentFolderId = Guid.NewGuid();
        var childFolders = new List<Folder>
        {
            CreateTestFolder(name: "Child 1", parentFolderId: parentFolderId),
            CreateTestFolder(name: "Child 2", parentFolderId: parentFolderId),
        };

        _mockRepository.Setup(p => p.GetChildFoldersAsync(parentFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childFolders);

        var service = CreateFolderService();

        // Act
        var result = await service.GetChildFoldersAsync(parentFolderId);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(p => p.ParentFolderId == parentFolderId)).IsTrue();
    }

    [Test]
    public async Task UpdateAsync_WithValidFolder_ShouldUpdate()
    {
        // Arrange
        var folder = CreateTestFolder();
        folder.Name = "Updated Name";

        _mockRepository.Setup(p => p.UpdateAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateFolderService();

        // Act
        await service.UpdateAsync(folder);

        // Assert
        await Assert.That(folder.ModifiedAt).IsNotNull();
        _mockRepository.Verify(p => p.UpdateAsync(folder, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_WithNullFolder_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateFolderService();

        // Act & Assert
        await Assert.That(async () => await service.UpdateAsync(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DeleteAsync_WithValidId_ShouldDeleteFolder()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        _mockRepository.Setup(p => p.DeleteAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateFolderService();

        // Act
        await service.DeleteAsync(folderId);

        // Assert
        _mockRepository.Verify(p => p.DeleteAsync(folderId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
