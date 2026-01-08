using System.Collections.ObjectModel;
using System.IO;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

public partial class XmlImportViewModel : ObservableObject
{
    private readonly IClipService _clipService;
    private readonly ICollectionTreeBuilder _collectionTreeBuilder;
    private readonly IConfigurationService _configurationService;
    private readonly IExportImportService _exportImportService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DoneCommand))]
    private bool _isComplete;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteXmlImportCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private string _processingLog = string.Empty;

    [ObservableProperty]
    private int _progressMaximum = 100;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteXmlImportCommand))]
    private CollectionTreeNode? _selectedCollection;

    [ObservableProperty]
    private string _xmlImportFilePath = string.Empty;

    public XmlImportViewModel(IExportImportService exportImportService,
        ICollectionTreeBuilder collectionTreeBuilder,
        IClipService clipService,
        IConfigurationService configurationService)
    {
        _exportImportService = exportImportService;
        _collectionTreeBuilder = collectionTreeBuilder;
        _clipService = clipService;
        _configurationService = configurationService;
    }

    /// <summary>
    /// Root nodes for the collection tree (excludes virtual and special collections).
    /// </summary>
    public ObservableCollection<TreeNodeBase> RootNodes { get; } = [];

    /// <summary>
    /// Gets whether a collection is selected.
    /// </summary>
    public bool HasSelection => SelectedCollection is not null;

    /// <summary>
    /// Gets the selected collection ID, or Empty if no selection (defaults to root).
    /// </summary>
    public Guid SelectedCollectionId => SelectedCollection?.Collection.Id ?? Guid.Empty;

    /// <summary>
    /// Gets the database key for the selected collection by traversing up the tree.
    /// </summary>
    public string? SelectedDatabaseKey
    {
        get
        {
            if (SelectedCollection == null)
                return null;

            // Traverse up the tree to find the DatabaseTreeNode
            var current = SelectedCollection.Parent;
            while (current != null)
            {
                if (current is DatabaseTreeNode dbNode)
                    return dbNode.DatabasePath;

                current = current.Parent;
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the default import directory from configuration.
    /// </summary>
    public string DefaultImportDirectory => _configurationService.Configuration.Export.GetResolvedImportDirectory();

    public bool? DialogResult { get; set; }

    public async Task InitializeAsync()
    {
        try
        {
            await LoadCollectionsTreeAsync();

            // Set default import directory if path is empty
            if (string.IsNullOrEmpty(XmlImportFilePath) && !string.IsNullOrEmpty(DefaultImportDirectory))
                XmlImportFilePath = DefaultImportDirectory;
        }
        catch (Exception ex)
        {
            ProcessingLog += $"Init error: {ex.Message}\n";
        }
    }

    private async Task LoadCollectionsTreeAsync()
    {
        RootNodes.Clear();

        // Exclude virtual and special collections - they don't store clips
        const TreeNodeType excludeTypes = TreeNodeType.VirtualCollection | TreeNodeType.SpecialCollection;
        var treeNodes = await _collectionTreeBuilder.BuildTreeAsync(excludeTypes);

        foreach (var node in treeNodes)
            RootNodes.Add(node);
    }

    public void SetXmlImportFilePath(string filePath) => XmlImportFilePath = filePath;

    private bool CanExecuteXmlImport() => !IsProcessing && !IsComplete;

    [RelayCommand(CanExecute = nameof(CanExecuteXmlImport))]
    private async Task ExecuteXmlImport()
    {
        if (string.IsNullOrEmpty(XmlImportFilePath))
        {
            ProcessingLog += "Error: XML import file not selected.\n";
            return;
        }

        if (!File.Exists(XmlImportFilePath))
        {
            ProcessingLog += "Error: XML import file not found.\n";
            return;
        }

        var databaseKey = SelectedDatabaseKey;
        if (string.IsNullOrEmpty(databaseKey))
        {
            ProcessingLog += "Error: Please select a target collection.\n";
            return;
        }

        IsProcessing = true;
        IsComplete = false;
        ProgressValue = 0;
        ProcessingLog += "Starting XML import...\n";

        // Create cancellation token source for this import operation
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // Step 1: Parse the XML file
            var importData = await _exportImportService.ImportFromXmlAsync(
                XmlImportFilePath,
                SelectedCollectionId,
                p => ProcessingLog += $"{p.Message}\n",
                _cancellationTokenSource.Token);

            if (importData.Clips.Count == 0)
            {
                ProcessingLog += "No clips to import.\n";
                IsComplete = true;
                return;
            }

            // Step 2: Save clips to the database
            ProgressMaximum = importData.Clips.Count;
            ProgressValue = 0;
            ProcessingLog += $"Saving {importData.Clips.Count} clip(s) to database...\n";

            var savedCount = 0;
            foreach (var item in importData.Clips)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                // Set the collection ID if a target was selected
                if (SelectedCollectionId != Guid.Empty)
                    item.CollectionId = SelectedCollectionId;

                await _clipService.CreateAsync(databaseKey, item);
                savedCount++;
                ProgressValue = savedCount;

                // Update progress every 10 clips or on last clip
                if (savedCount % 10 == 0 || savedCount == importData.Clips.Count)
                    ProcessingLog += $"Saved {savedCount} of {importData.Clips.Count} clip(s)...\n";
            }

            // Update last import date and save directory
            _configurationService.Configuration.Export.DateLastImport = DateTime.Now;
            _configurationService.Configuration.Export.ImportDirectory = Path.GetDirectoryName(XmlImportFilePath);
            await _configurationService.SaveAsync();

            ProcessingLog += $"Import complete: {savedCount} clip(s) imported successfully.\n";
            IsComplete = true;
        }
        catch (OperationCanceledException)
        {
            ProcessingLog += "Import cancelled by user.\n";
        }
        catch (Exception ex)
        {
            ProcessingLog += $"XML import failed: {ex.Message}\n";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Aborts the import operation if running, or closes the dialog if not.
    /// </summary>
    [RelayCommand]
    private void Abort()
    {
        if (IsProcessing)
        {
            // Cancel the running import operation
            _cancellationTokenSource?.Cancel();
            ProcessingLog += "Cancelling import...\n";
        }
        else
        {
            // Close the dialog
            DialogResult = false;
        }
    }

    private bool CanDone() => IsComplete;

    /// <summary>
    /// Closes the dialog after successful completion.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDone))]
    private void Done() => DialogResult = true;
}
