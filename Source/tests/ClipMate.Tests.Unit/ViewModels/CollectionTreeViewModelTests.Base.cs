using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Base class for CollectionTreeViewModel tests containing shared setup.
/// </summary>
public class CollectionTreeViewModelTests
{
    private readonly Mock<ICollectionService> _mockCollectionService;
    private readonly Mock<IClipService> _mockClipService;
    private readonly Mock<ICollectionTreeBuilder> _mockCollectionTreeBuilder;
    private readonly Mock<IConfigurationService> _mockConfigurationService;
    private readonly Mock<IFolderService> _mockFolderService;
    private readonly Mock<ILogger<CollectionTreeViewModel>> _mockLogger;
    private readonly Mock<IMessenger> _mockMessenger;
    private readonly CollectionTreeViewModel _viewModel;

    public CollectionTreeViewModelTests()
    {
        _mockCollectionService = new Mock<ICollectionService>();
        _mockFolderService = new Mock<IFolderService>();
        _mockClipService = new Mock<IClipService>();
        _mockConfigurationService = new Mock<IConfigurationService>();
        _mockMessenger = new Mock<IMessenger>();
        _mockLogger = new Mock<ILogger<CollectionTreeViewModel>>();
        _mockCollectionTreeBuilder = new Mock<ICollectionTreeBuilder>();

        // Setup default configuration with a single database
        var config = new ClipMateConfiguration
        {
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                ["default"] = new()
                {
                    Name = "My Clips",
                    FilePath = "C:\\test\\clipmate.db",
                    AutoLoad = true,
                },
            },
            DefaultDatabase = "default",
        };

        _mockConfigurationService.Setup(p => p.Configuration).Returns(config);

        _viewModel = new CollectionTreeViewModel(
            _mockCollectionService.Object,
            _mockFolderService.Object,
            _mockClipService.Object,
            _mockConfigurationService.Object,
            _mockMessenger.Object,
            _mockCollectionTreeBuilder.Object,
            _mockLogger.Object,
            new SearchResultsCache());
    }
}
