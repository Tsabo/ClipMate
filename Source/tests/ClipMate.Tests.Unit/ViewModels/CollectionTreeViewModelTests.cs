using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for CollectionTreeViewModel.
/// </summary>
public class CollectionTreeViewModelTests
{
    private readonly Mock<ICollectionService> _mockCollectionService;
    private readonly Mock<IFolderService> _mockFolderService;
    private readonly Mock<IConfigurationService> _mockConfigurationService;
    private readonly Mock<IMessenger> _mockMessenger;
    private readonly Mock<ILogger<CollectionTreeViewModel>> _mockLogger;
    private readonly CollectionTreeViewModel _viewModel;

    public CollectionTreeViewModelTests()
    {
        _mockCollectionService = new Mock<ICollectionService>();
        _mockFolderService = new Mock<IFolderService>();
        _mockConfigurationService = new Mock<IConfigurationService>();
        _mockMessenger = new Mock<IMessenger>();
        _mockLogger = new Mock<ILogger<CollectionTreeViewModel>>();
        
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
            _mockConfigurationService.Object,
            _mockMessenger.Object,
            _mockLogger.Object);
    }

    [Test]
    public async Task Constructor_WithNullCollectionService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => 
            new CollectionTreeViewModel(null!, _mockFolderService.Object, _mockConfigurationService.Object, _mockMessenger.Object, _mockLogger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullFolderService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => 
            new CollectionTreeViewModel(_mockCollectionService.Object, null!, _mockConfigurationService.Object, _mockMessenger.Object, _mockLogger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
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
        await Assert.That(_viewModel.RootNodes.Count).IsEqualTo(1);
        var database = _viewModel.RootNodes[0];
        await Assert.That(database).IsTypeOf<DatabaseTreeNode>();
        await Assert.That(database.Children.Count).IsEqualTo(2);
        
        var workCollection = database.Children[0];
        await Assert.That(workCollection).IsTypeOf<CollectionTreeNode>();
        await Assert.That(((CollectionTreeNode)workCollection).Name).IsEqualTo("Work");
        await Assert.That(((CollectionTreeNode)workCollection).Children.OfType<FolderTreeNode>().Count()).IsEqualTo(1);
        await Assert.That(((CollectionTreeNode)workCollection).Children.OfType<FolderTreeNode>().First().Name).IsEqualTo("Projects");
        
        var personalCollection = database.Children[1];
        await Assert.That(personalCollection).IsTypeOf<CollectionTreeNode>();
        await Assert.That(((CollectionTreeNode)personalCollection).Name).IsEqualTo("Personal");
        await Assert.That(((CollectionTreeNode)personalCollection).Children.OfType<FolderTreeNode>().Count()).IsEqualTo(0);
    }

    [Test]
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
        await Assert.That(_viewModel.RootNodes.Count).IsEqualTo(1);
        var database = _viewModel.RootNodes[0];
        await Assert.That(database).IsTypeOf<DatabaseTreeNode>();
        var collectionNode = database.Children[0];
        await Assert.That(collectionNode).IsTypeOf<CollectionTreeNode>();
        
        var folders = ((CollectionTreeNode)collectionNode).Children.OfType<FolderTreeNode>().ToList();
        await Assert.That(folders.Count).IsEqualTo(1);
        var rootFolderNode = folders[0];
        await Assert.That(rootFolderNode.Name).IsEqualTo("Projects");
        
        var subFolders = rootFolderNode.Children.OfType<FolderTreeNode>().ToList();
        await Assert.That(subFolders.Count).IsEqualTo(1);
        await Assert.That(subFolders[0].Name).IsEqualTo("2024");
    }

    [Test]
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
        await Assert.That(_viewModel.RootNodes.Count).IsEqualTo(1);
        var database = _viewModel.RootNodes[0];
        await Assert.That(database).IsTypeOf<DatabaseTreeNode>();
        var collection = database.Children[0];
        await Assert.That(collection).IsTypeOf<CollectionTreeNode>();
        await Assert.That(((CollectionTreeNode)collection).Name).IsEqualTo("New Collection");
        _mockCollectionService.Verify(s => s.CreateAsync("New Collection", "Test description", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
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
        await Assert.That(_viewModel.RootNodes.Count).IsEqualTo(1);
        var database = _viewModel.RootNodes[0];
        await Assert.That(database).IsTypeOf<DatabaseTreeNode>();
        var collectionNode = database.Children[0];
        await Assert.That(collectionNode).IsTypeOf<CollectionTreeNode>();
        await Assert.That(((CollectionTreeNode)collectionNode).Children.OfType<FolderTreeNode>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task SelectedNode_WhenSet_ShouldRaisePropertyChanged()
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
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(_viewModel.SelectedNode).IsEqualTo(collectionNode);
    }

    [Test]
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
        await Assert.That(_viewModel.RootNodes.Count).IsEqualTo(1);
        var database = _viewModel.RootNodes[0];
        await Assert.That(database).IsTypeOf<DatabaseTreeNode>();
        await Assert.That(database.Children.OfType<CollectionTreeNode>().Count()).IsEqualTo(0);
    }

    [Test]
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
