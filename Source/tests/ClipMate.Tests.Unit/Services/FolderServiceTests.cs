using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public class FolderServiceTests
{
    private readonly IDatabaseContextFactory _mockContextFactory;
    private readonly ICollectionService _mockCollectionService;
    private readonly IFolderRepository _mockRepository;

    public FolderServiceTests()
    {
        _mockRepository = A.Fake<IFolderRepository>();
        _mockContextFactory = A.Fake<IDatabaseContextFactory>();
        _mockCollectionService = A.Fake<ICollectionService>();
        
        A.CallTo(() => _mockContextFactory.GetFolderRepository(A<string>.Ignored)).Returns(_mockRepository);
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns("test-db");
    }

    private IFolderService CreateFolderService() => new FolderService(_mockContextFactory, _mockCollectionService);

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

        A.CallTo(() => _mockRepository.CreateAsync(A<Folder>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily((Folder f, CancellationToken _) => f);

        var service = CreateFolderService();

        // Act
        var result = await service.CreateAsync(name, collectionId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo(name);
        await Assert.That(result.CollectionId).IsEqualTo(collectionId);
        await Assert.That(result.ParentFolderId).IsNull();
        await Assert.That(result.Id).IsNotEqualTo(Guid.Empty);
        A.CallTo(() => _mockRepository.CreateAsync(A<Folder>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task CreateAsync_WithParentFolder_ShouldSetParentFolderId()
    {
        // Arrange
        const string name = "Subfolder";
        var collectionId = Guid.NewGuid();
        var parentFolderId = Guid.NewGuid();

        A.CallTo(() => _mockRepository.CreateAsync(A<Folder>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily((Folder f, CancellationToken _) => f);

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
        A.CallTo(() => _mockRepository.GetByIdAsync(folderId, A<CancellationToken>.Ignored))
            .Returns(expectedFolder);

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
        A.CallTo(() => _mockRepository.GetByIdAsync(folderId, A<CancellationToken>.Ignored))
            .Returns((Folder?)null);

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

        A.CallTo(() => _mockRepository.GetByCollectionAsync(collectionId, A<CancellationToken>.Ignored))
            .Returns(folders);

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

        A.CallTo(() => _mockRepository.GetRootFoldersAsync(collectionId, A<CancellationToken>.Ignored))
            .Returns(rootFolders);

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

        A.CallTo(() => _mockRepository.GetChildFoldersAsync(parentFolderId, A<CancellationToken>.Ignored))
            .Returns(childFolders);

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

        A.CallTo(() => _mockRepository.UpdateAsync(A<Folder>.Ignored, A<CancellationToken>.Ignored))
            .Returns(true);

        var service = CreateFolderService();

        // Act
        await service.UpdateAsync(folder);

        // Assert
        await Assert.That(folder.ModifiedAt).IsNotNull();
        A.CallTo(() => _mockRepository.UpdateAsync(folder, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
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
        A.CallTo(() => _mockRepository.DeleteAsync(folderId, A<CancellationToken>.Ignored))
            .Returns(true);

        var service = CreateFolderService();

        // Act
        await service.DeleteAsync(folderId);

        // Assert
        A.CallTo(() => _mockRepository.DeleteAsync(folderId, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }
}
