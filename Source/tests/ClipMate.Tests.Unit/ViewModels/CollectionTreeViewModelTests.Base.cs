using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Base class for CollectionTreeViewModel tests containing shared setup.
/// </summary>
public partial class CollectionTreeViewModelTests
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
        
        // Create a mock service scope factory that returns our mock services
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(ICollectionService))).Returns(_mockCollectionService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IFolderService))).Returns(_mockFolderService.Object);
        mockServiceScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(mockServiceScope.Object);
        
        _viewModel = new CollectionTreeViewModel(
            mockServiceScopeFactory.Object,
            _mockConfigurationService.Object,
            _mockMessenger.Object,
            _mockLogger.Object);
    }
}
