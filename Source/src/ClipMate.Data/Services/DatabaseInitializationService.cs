using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service responsible for initializing the database with default collections and folders.
/// </summary>
public class DatabaseInitializationService
{
    private readonly ICollectionService _collectionService;
    private readonly IFolderRepository _folderRepository;
    private readonly ILogger<DatabaseInitializationService>? _logger;

    public DatabaseInitializationService(
        ICollectionService collectionService,
        IFolderRepository folderRepository,
        ILogger<DatabaseInitializationService>? logger = null)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _folderRepository = folderRepository ?? throw new ArgumentNullException(nameof(folderRepository));
        _logger = logger;
    }

    /// <summary>
    /// Initializes the database with default collection and folders if they don't exist.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Initializing database with default data");

            // Check if any collections exist
            var collections = await _collectionService.GetAllAsync(cancellationToken);
            
            if (collections.Count == 0)
            {
                _logger?.LogInformation("No collections found, creating default collection");
                
                // Create default "My Clips" collection
                var defaultCollection = await _collectionService.CreateAsync("My Clips", null, cancellationToken);
                
                // Create default folders in the collection
                await CreateDefaultFoldersAsync(defaultCollection.Id, cancellationToken);
            }
            else
            {
                _logger?.LogInformation("Collections already exist, checking for default folders");
                
                // Ensure each collection has default folders
                foreach (var collection in collections)
                {
                    var folders = await _folderRepository.GetRootFoldersAsync(collection.Id, cancellationToken);
                    
                    // If collection has no folders, create default ones
                    if (folders.Count == 0)
                    {
                        _logger?.LogInformation("Creating default folders for collection {CollectionName}", collection.Name);
                        await CreateDefaultFoldersAsync(collection.Id, cancellationToken);
                    }
                }
            }

            _logger?.LogInformation("Database initialization complete");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize database with default data");
            throw;
        }
    }

    /// <summary>
    /// Creates the default folder structure for a collection (ClipMate 7.5 style).
    /// </summary>
    private async Task CreateDefaultFoldersAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        var defaultFolders = new[]
        {
            new { Name = "InBox", SortOrder = 1, Icon = "📥" },
            new { Name = "Safe", SortOrder = 2, Icon = "🔒" },
            new { Name = "Overflow", SortOrder = 3, Icon = "📦" },
            new { Name = "Samples", SortOrder = 4, Icon = "📄" },
            new { Name = "Virtual", SortOrder = 5, Icon = "🔗" },
            new { Name = "Trash Can", SortOrder = 6, Icon = "🗑️" },
            new { Name = "Search Results", SortOrder = 7, Icon = "🔍" }
        };

        foreach (var folderDef in defaultFolders)
        {
            var folder = new Folder
            {
                Id = Guid.NewGuid(),
                Name = folderDef.Name,
                CollectionId = collectionId,
                ParentFolderId = null, // Root level
                SortOrder = folderDef.SortOrder,
                IsSystemFolder = true, // Mark as system folder (cannot be deleted)
                IconName = folderDef.Icon,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };

            await _folderRepository.CreateAsync(folder, cancellationToken);
            
            _logger?.LogDebug("Created default folder: {FolderName} in collection {CollectionId}", 
                folder.Name, collectionId);
        }
    }
}
