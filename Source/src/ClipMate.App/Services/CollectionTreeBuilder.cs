using System.IO;
using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services;

internal class CollectionTreeBuilder : ICollectionTreeBuilder
{
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseManager _databaseManager;
    private readonly IFolderService _folderService;
    private readonly ILogger<CollectionTreeBuilder> _logger;

    public CollectionTreeBuilder(IConfigurationService configurationService,
        IFolderService folderService,
        IDatabaseManager databaseManager,
        ILogger<CollectionTreeBuilder> logger)
    {
        _configurationService = configurationService;
        _folderService = folderService;
        _databaseManager = databaseManager;
        _logger = logger;
    }

    public async Task<IEnumerable<TreeNodeBase>> BuildTreeAsync(TreeNodeType excludeNodes, CancellationToken cancellationToken = default)
    {
        var result = new List<TreeNodeBase>();

        // Load configuration to get database definitions
        var configuration = _configurationService.Configuration;

        // Create a database node for each configured database
        if (configuration.Databases.Count <= 0)
            return result;

        _logger.LogInformation("Creating database nodes for {Count} databases: {DatabaseKeys}",
            configuration.Databases.Count,
            string.Join(", ", configuration.Databases.Keys));

        // Get database manager to access all loaded databases
        foreach (var (databaseId, databaseConfig) in configuration.Databases)
        {
            // Check if database file exists
            var dbFile = Environment.ExpandEnvironmentVariables(databaseConfig.FilePath);
            var hasError = !File.Exists(dbFile);

            // Create database node with title from configuration and error status
            var databaseNode = new DatabaseTreeNode(databaseConfig.Name, databaseId, hasError);

            // Load collections for this database if it exists
            if (!hasError)
            {
                try
                {
                    // Get the database context for this specific database
                    var dbContext = _databaseManager.GetDatabaseContext(databaseId);

                    if (dbContext != null)
                    {
                        // Load collections directly from this database context
                        var allCollections = await dbContext.Collections.ToListAsync(cancellationToken);

                        // Separate regular collections from virtual ones
                        var regularCollections = allCollections.Where(p => !p.IsVirtual).ToList();
                        var virtualCollections = allCollections.Where(p => p.IsVirtual).ToList();

                        _logger.LogInformation("Loaded {RegularCount} regular and {VirtualCount} virtual collections from database {DatabaseName}",
                            regularCollections.Count, virtualCollections.Count, databaseConfig.Name);

                        // Determine sort order based on global preference
                        var sortByAlpha = _configurationService.Configuration.Preferences.SortCollectionsAlphabetically;
                        var orderedCollections = sortByAlpha
                            ? regularCollections.OrderBy(p => p.Title)
                            : regularCollections.OrderBy(p => p.SortKey);

                        // Add regular collections to database node
                        foreach (var item in orderedCollections)
                        {
                            var collectionNode = new CollectionTreeNode(item)
                            {
                                Parent = databaseNode,
                            };

                            await LoadFoldersAsync(collectionNode, cancellationToken);

                            databaseNode.Children.Add(collectionNode);
                        }

                        // Add virtual collections container if any virtual collections exist
                        if (!excludeNodes.HasFlag(TreeNodeType.VirtualCollection) && virtualCollections.Count > 0)
                        {
                            var virtualContainer = new VirtualCollectionsContainerNode
                            {
                                Parent = databaseNode,
                            };

                            // Virtual collections also respect the SortByAlpha preference
                            var orderedVirtualCollections = sortByAlpha
                                ? virtualCollections.OrderBy(p => p.Title)
                                : virtualCollections.OrderBy(p => p.SortKey);

                            // Add other virtual collections
                            foreach (var item in orderedVirtualCollections)
                            {
                                var virtualNode = new VirtualCollectionTreeNode(item)
                                {
                                    Parent = virtualContainer,
                                };

                                virtualContainer.Children.Add(virtualNode);
                            }

                            databaseNode.Children.Add(virtualContainer);
                        }

                        if (!excludeNodes.HasFlag(TreeNodeType.SpecialCollection))
                        {
                            // Add Trashcan at bottom of collections (after all regular collections)
                            // Trashcan is read-only (rejects new clips), which makes it display in red
                            var trashcanNode = new TrashcanVirtualCollectionNode(databaseConfig.FilePath)
                            {
                                Parent = databaseNode,
                            };

                            databaseNode.Children.Add(trashcanNode);
                        }
                    }
                    else
                        _logger.LogWarning("Database context not loaded for: {DatabaseName}", databaseConfig.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load collections from database: {DatabaseName}", databaseConfig.Name);
                }
            }

            // Expand the default database node by default
            if (databaseId == configuration.DefaultDatabase)
                databaseNode.IsExpanded = true;

            result.Add(databaseNode);
        }

        return result;
    }

    /// <summary>
    /// Loads the folder hierarchy for a collection.
    /// </summary>
    private async Task LoadFoldersAsync(CollectionTreeNode collectionNode, CancellationToken cancellationToken)
    {
        var rootFolders = await _folderService.GetRootFoldersAsync(collectionNode.Collection.Id, cancellationToken);

        foreach (var item in rootFolders)
        {
            var folderNode = new FolderTreeNode(item)
            {
                Parent = collectionNode,
            };

            await LoadSubFoldersAsync(folderNode, cancellationToken);

            collectionNode.Children.Add(folderNode);
        }
    }

    /// <summary>
    /// Recursively loads subfolders for a folder node.
    /// </summary>
    private async Task LoadSubFoldersAsync(FolderTreeNode folderNode, CancellationToken cancellationToken)
    {
        var subFolders = await _folderService.GetChildFoldersAsync(folderNode.Folder.Id, cancellationToken);

        foreach (var item in subFolders)
        {
            var subFolderNode = new FolderTreeNode(item)
            {
                Parent = folderNode,
            };

            await LoadSubFoldersAsync(subFolderNode, cancellationToken);

            folderNode.Children.Add(subFolderNode);
        }
    }
}
