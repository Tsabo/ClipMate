using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Moq;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for CollectionTreeViewModel.
/// </summary>
public class CollectionTreeViewModelTests
{
    private readonly Mock<ICollectionService> _mockCollectionService;
    private readonly Mock<IFolderService> _mockFolderService;
    private readonly Mock<IConfigurationService> _mockConfigurationService;
    private readonly CollectionTreeViewModel _viewModel;

    public CollectionTreeViewModelTests()
    {
        _mockCollectionService = new Mock<ICollectionService>();
        _mockFolderService = new Mock<IFolderService>();
        _mockConfigurationService = new Mock<IConfigurationService>();
        
        // Setup default configuration with a single database
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                ["default"] = new DatabaseConfiguration
                {
                    Name = "My Clips",
                    Directory = "C:\\test",
                    AutoLoad = true
                }
            },
            DefaultDatabase = "default"
        };
        _mockConfigurationService.Setup(x => x.Configuration).Returns(config);
        
        _viewModel = new CollectionTreeViewModel(
            _mockCollectionService.Object, 
            _mockFolderService.Object,
            _mockConfigurationService.Object);
    }

    [Fact]
    public void Constructor_WithNullCollectionService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new CollectionTreeViewModel(null!, _mockFolderService.Object, _mockConfigurationService.Object));
    }

    [Fact]
    public void Constructor_WithNullFolderService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new CollectionTreeViewModel(_mockCollectionService.Object, null!, _mockConfigurationService.Object));
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadCollectionsAndRootFolders()
    {
        // Arrange
        var collection1 = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Work",
            Description = "Work clips",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var collection2 = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Personal",
            Description = "Personal clips",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        var folder1 = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "Projects",
            CollectionId = collection1.Id,
            ParentFolderId = null,
            CreatedAt = DateTime.UtcNow
        };

        _mockCollectionService
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Collection> { collection1, collection2 });

        _mockFolderService
            .Setup(s => s.GetRootFoldersAsync(collection1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Folder> { folder1 });

        _mockFolderService
            .Setup(s => s.GetRootFoldersAsync(collection2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Folder>());

        // Set up GetChildFoldersAsync to return empty list by default
        _mockFolderService
            .Setup(s => s.GetChildFoldersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Folder>());

        // Act
        await _viewModel.LoadAsync();

        // Assert
        _viewModel.RootNodes.Count.ShouldBe(1);
        var database = _viewModel.RootNodes[0].ShouldBeOfType<DatabaseTreeNode>();
        database.Children.Count.ShouldBe(2);
        
        var workCollection = database.Children[0].ShouldBeOfType<CollectionTreeNode>();
        workCollection.Name.ShouldBe("Work");
        workCollection.Children.OfType<FolderTreeNode>().Count().ShouldBe(1);
        workCollection.Children.OfType<FolderTreeNode>().First().Name.ShouldBe("Projects");
        
        var personalCollection = database.Children[1].ShouldBeOfType<CollectionTreeNode>();
        personalCollection.Name.ShouldBe("Personal");
        personalCollection.Children.OfType<FolderTreeNode>().Count().ShouldBe(0);
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadNestedFolders()
    {
        // Arrange
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Work",
            CreatedAt = DateTime.UtcNow
        };

        var rootFolder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "Projects",
            CollectionId = collection.Id,
            ParentFolderId = null,
            CreatedAt = DateTime.UtcNow
        };

        var childFolder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "2024",
            CollectionId = collection.Id,
            ParentFolderId = rootFolder.Id,
            CreatedAt = DateTime.UtcNow
        };

        _mockCollectionService
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Collection> { collection });

        _mockFolderService
            .Setup(s => s.GetRootFoldersAsync(collection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Folder> { rootFolder });

        // Set up default to return empty list for other folders
        _mockFolderService
            .Setup(s => s.GetChildFoldersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Folder>());

        // Override for specific folder
        _mockFolderService
            .Setup(s => s.GetChildFoldersAsync(rootFolder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Folder> { childFolder });

        // Act
        await _viewModel.LoadAsync();

        // Assert
        _viewModel.RootNodes.Count.ShouldBe(1);
        var database = _viewModel.RootNodes[0].ShouldBeOfType<DatabaseTreeNode>();
        var collectionNode = database.Children[0].ShouldBeOfType<CollectionTreeNode>();
        
        var folders = collectionNode.Children.OfType<FolderTreeNode>().ToList();
        folders.Count.ShouldBe(1);
        var rootFolderNode = folders[0];
        rootFolderNode.Name.ShouldBe("Projects");
        
        var subFolders = rootFolderNode.Children.OfType<FolderTreeNode>().ToList();
        subFolders.Count.ShouldBe(1);
        subFolders[0].Name.ShouldBe("2024");
    }

    [Fact]
    public async Task CreateCollectionCommand_ShouldCreateCollectionAndReload()
    {
        // Arrange
        var newCollection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "New Collection",
            Description = "Test description",
            CreatedAt = DateTime.UtcNow
        };

        _mockCollectionService
            .Setup(s => s.CreateAsync("New Collection", "Test description", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newCollection);

        _mockCollectionService
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Collection> { newCollection });

        _mockFolderService
            .Setup(s => s.GetRootFoldersAsync(newCollection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Folder>());

        // Act
        await _viewModel.CreateCollectionCommand.ExecuteAsync(("New Collection", "Test description"));

        // Assert
        _viewModel.RootNodes.Count.ShouldBe(1);
        var database = _viewModel.RootNodes[0].ShouldBeOfType<DatabaseTreeNode>();
        var collection = database.Children[0].ShouldBeOfType<CollectionTreeNode>();
        collection.Name.ShouldBe("New Collection");
        _mockCollectionService.Verify(s => s.CreateAsync("New Collection", "Test description", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateFolderCommand_ShouldCreateFolderAndReload()
    {
        // Arrange
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Work",
            CreatedAt = DateTime.UtcNow
        };

        var newFolder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "New Folder",
            CollectionId = collection.Id,
            ParentFolderId = null,
            CreatedAt = DateTime.UtcNow
        };

        _mockFolderService
            .Setup(s => s.CreateAsync("New Folder", collection.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFolder);

        _mockCollectionService
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Collection> { collection });

        _mockFolderService
            .Setup(s => s.GetRootFoldersAsync(collection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Folder> { newFolder });

        // Set up default to return empty list for child folders
        _mockFolderService
            .Setup(s => s.GetChildFoldersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Folder>());

        // Act
        await _viewModel.CreateFolderCommand.ExecuteAsync(("New Folder", collection.Id, null));

        // Assert
        _mockFolderService.Verify(s => s.CreateAsync("New Folder", collection.Id, null, It.IsAny<CancellationToken>()), Times.Once);
        _viewModel.RootNodes.Count.ShouldBe(1);
        var database = _viewModel.RootNodes[0].ShouldBeOfType<DatabaseTreeNode>();
        var collectionNode = database.Children[0].ShouldBeOfType<CollectionTreeNode>();
        collectionNode.Children.OfType<FolderTreeNode>().Count().ShouldBe(1);
    }

    [Fact]
    public void SelectedNode_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "Work",
            CreatedAt = DateTime.UtcNow
        };

        var collectionNode = new CollectionTreeNode(collection);
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.SelectedNode))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        _viewModel.SelectedNode = collectionNode;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        _viewModel.SelectedNode.ShouldBe(collectionNode);
    }

    [Fact]
    public async Task DeleteCollectionCommand_ShouldDeleteCollectionAndReload()
    {
        // Arrange
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "To Delete",
            CreatedAt = DateTime.UtcNow
        };

        _mockCollectionService
            .Setup(s => s.DeleteAsync(collection.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockCollectionService
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Collection>());

        // Act
        await _viewModel.DeleteCollectionCommand.ExecuteAsync(collection.Id);

        // Assert
        _mockCollectionService.Verify(s => s.DeleteAsync(collection.Id, It.IsAny<CancellationToken>()), Times.Once);
        _viewModel.RootNodes.Count.ShouldBe(1);
        var database = _viewModel.RootNodes[0].ShouldBeOfType<DatabaseTreeNode>();
        database.Children.OfType<CollectionTreeNode>().Count().ShouldBe(0);
    }

    [Fact]
    public async Task DeleteFolderCommand_ShouldDeleteFolderAndReload()
    {
        // Arrange
        var folderId = Guid.NewGuid();

        _mockFolderService
            .Setup(s => s.DeleteAsync(folderId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockCollectionService
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Collection>());

        // Act
        await _viewModel.DeleteFolderCommand.ExecuteAsync(folderId);

        // Assert
        _mockFolderService.Verify(s => s.DeleteAsync(folderId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
