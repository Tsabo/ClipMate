using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Moq;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Unit.Services;

public class FolderServiceTests
{
    private readonly Mock<IFolderRepository> _mockRepository;

    public FolderServiceTests()
    {
        _mockRepository = new Mock<IFolderRepository>();
    }

    private IFolderService CreateFolderService()
    {
        return new FolderService(_mockRepository.Object);
    }

    private Folder CreateTestFolder(Guid? id = null, string name = "Test Folder", Guid? collectionId = null, Guid? parentFolderId = null)
    {
        return new Folder
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            CollectionId = collectionId ?? Guid.NewGuid(),
            ParentFolderId = parentFolderId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task CreateAsync_WithValidParameters_ShouldCreateAndReturnFolder()
    {
        // Arrange
        var name = "New Folder";
        var collectionId = Guid.NewGuid();
        
        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);

        var service = CreateFolderService();

        // Act
        var result = await service.CreateAsync(name, collectionId);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(name);
        result.CollectionId.ShouldBe(collectionId);
        result.ParentFolderId.ShouldBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithParentFolder_ShouldSetParentFolderId()
    {
        // Arrange
        var name = "Subfolder";
        var collectionId = Guid.NewGuid();
        var parentFolderId = Guid.NewGuid();
        
        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);

        var service = CreateFolderService();

        // Act
        var result = await service.CreateAsync(name, collectionId, parentFolderId);

        // Assert
        result.ParentFolderId.ShouldBe(parentFolderId);
    }

    [Fact]
    public async Task CreateAsync_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateFolderService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () => await service.CreateAsync(null!, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnFolder()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        var expectedFolder = CreateTestFolder(folderId);
        _mockRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFolder);

        var service = CreateFolderService();

        // Act
        var result = await service.GetByIdAsync(folderId);

        // Assert
        result.ShouldNotBeNull();
        result!.Id.ShouldBe(folderId);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder?)null);

        var service = CreateFolderService();

        // Act
        var result = await service.GetByIdAsync(folderId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByCollectionAsync_ShouldReturnAllFoldersInCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var folders = new List<Folder>
        {
            CreateTestFolder(name: "Folder 1", collectionId: collectionId),
            CreateTestFolder(name: "Folder 2", collectionId: collectionId),
            CreateTestFolder(name: "Folder 3", collectionId: collectionId)
        };

        _mockRepository.Setup(r => r.GetByCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folders);

        var service = CreateFolderService();

        // Act
        var result = await service.GetByCollectionAsync(collectionId);

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldAllBe(f => f.CollectionId == collectionId);
    }

    [Fact]
    public async Task GetRootFoldersAsync_ShouldReturnOnlyRootFolders()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var rootFolders = new List<Folder>
        {
            CreateTestFolder(name: "Root 1", collectionId: collectionId, parentFolderId: null),
            CreateTestFolder(name: "Root 2", collectionId: collectionId, parentFolderId: null)
        };

        _mockRepository.Setup(r => r.GetRootFoldersAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rootFolders);

        var service = CreateFolderService();

        // Act
        var result = await service.GetRootFoldersAsync(collectionId);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(f => f.ParentFolderId == null);
    }

    [Fact]
    public async Task GetChildFoldersAsync_ShouldReturnChildFolders()
    {
        // Arrange
        var parentFolderId = Guid.NewGuid();
        var childFolders = new List<Folder>
        {
            CreateTestFolder(name: "Child 1", parentFolderId: parentFolderId),
            CreateTestFolder(name: "Child 2", parentFolderId: parentFolderId)
        };

        _mockRepository.Setup(r => r.GetChildFoldersAsync(parentFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childFolders);

        var service = CreateFolderService();

        // Act
        var result = await service.GetChildFoldersAsync(parentFolderId);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(f => f.ParentFolderId == parentFolderId);
    }

    [Fact]
    public async Task UpdateAsync_WithValidFolder_ShouldUpdate()
    {
        // Arrange
        var folder = CreateTestFolder();
        folder.Name = "Updated Name";

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateFolderService();

        // Act
        await service.UpdateAsync(folder);

        // Assert
        folder.ModifiedAt.ShouldNotBeNull();
        _mockRepository.Verify(r => r.UpdateAsync(folder, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNullFolder_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateFolderService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () => await service.UpdateAsync(null!));
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_ShouldDeleteFolder()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        _mockRepository.Setup(r => r.DeleteAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateFolderService();

        // Act
        await service.DeleteAsync(folderId);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(folderId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
