using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Base class for CollectionTreeViewModel tests containing shared setup.
/// </summary>
public class CollectionTreeViewModelTests
{
    private readonly ICollectionService _mockCollectionService;
    private readonly IClipService _mockClipService;
    private readonly ICollectionTreeBuilder _mockCollectionTreeBuilder;
    private readonly IConfigurationService _mockConfigurationService;
    private readonly IFolderService _mockFolderService;
    private readonly ILogger<CollectionTreeViewModel> _mockLogger;
    private readonly IMessenger _mockMessenger;
    private readonly CollectionTreeViewModel _viewModel;

    public CollectionTreeViewModelTests()
    {
        _mockCollectionService = A.Fake<ICollectionService>();
        _mockFolderService = A.Fake<IFolderService>();
        _mockClipService = A.Fake<IClipService>();
        _mockConfigurationService = A.Fake<IConfigurationService>();
        _mockMessenger = A.Fake<IMessenger>();
        _mockLogger = A.Fake<ILogger<CollectionTreeViewModel>>();
        _mockCollectionTreeBuilder = A.Fake<ICollectionTreeBuilder>();

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

        A.CallTo(() => _mockConfigurationService.Configuration).Returns(config);

        _viewModel = new CollectionTreeViewModel(
            _mockCollectionService,
            _mockFolderService,
            _mockClipService,
            _mockConfigurationService,
            _mockMessenger,
            _mockCollectionTreeBuilder,
            _mockLogger,
            new SearchResultsCache(),
            A.Fake<IDatabaseIdService>(),
            A.Fake<IRemoteDatabaseService>(),
            A.Fake<IRemoteDatabaseTreeBuilder>(),
            A.Fake<ISharedDatabaseService>());
    }
}
