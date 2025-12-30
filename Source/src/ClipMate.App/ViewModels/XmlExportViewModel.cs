using System.IO;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Export;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

public partial class XmlExportViewModel : ObservableObject
{
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly IConfigurationService _configurationService;
    private readonly IExportImportService _exportImportService;
    private CancellationTokenSource? _cancellationTokenSource;
    private Guid? _collectionId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DoneCommand))]
    private bool _isComplete;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteXmlExportCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private string _processingLog = string.Empty;

    [ObservableProperty]
    private int _progressMaximum = 100;

    [ObservableProperty]
    private int _progressValue;

    private List<Clip> _selectedClips = [];

    [ObservableProperty]
    private string _xmlExportFilePath = string.Empty;

    [ObservableProperty]
    private bool _xmlExportIncludeCollections = true;

    public XmlExportViewModel(IExportImportService exportImportService,
        IClipService clipService,
        ICollectionService collectionService,
        IConfigurationService configurationService)
    {
        _exportImportService = exportImportService;
        _clipService = clipService;
        _collectionService = collectionService;
        _configurationService = configurationService;
    }

    public bool? DialogResult { get; set; }

    /// <summary>
    /// Gets the count of selected clips for confirmation dialogs.
    /// </summary>
    public int SelectedClipCount => _selectedClips.Count;

    /// <summary>
    /// Gets the collection name for confirmation dialogs.
    /// </summary>
    public string CollectionName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the collection ID for loading all clips if user chooses to export entire collection.
    /// </summary>
    public Guid? CollectionId => _collectionId;

    /// <summary>
    /// Initializes the ViewModel with selected clips and collection info.
    /// </summary>
    /// <param name="selectedClips">The clips to export.</param>
    /// <param name="collectionName">The name of the current collection.</param>
    /// <param name="collectionId">The ID of the current collection (for loading all clips if needed).</param>
    public void Initialize(IEnumerable<Clip> selectedClips, string collectionName, Guid? collectionId)
    {
        _selectedClips = selectedClips.ToList();
        CollectionName = collectionName;
        _collectionId = collectionId;

        // Show selected item count
        ProcessingLog = $"{_selectedClips.Count} Items Selected.\n";

        // Generate default filename
        GenerateDefaultFilePath();
    }

    /// <summary>
    /// Updates the selected clips to export the entire collection.
    /// </summary>
    public async Task LoadEntireCollectionAsync()
    {
        if (_collectionId == null)
            return;

        try
        {
            var databaseKey = _collectionService.GetActiveDatabaseKey();
            if (string.IsNullOrEmpty(databaseKey))
            {
                ProcessingLog += "Error: No active database.\n";
                return;
            }

            var clips = await _clipService.GetByCollectionAsync(databaseKey, _collectionId.Value);
            _selectedClips = clips.ToList();

            ProcessingLog = $"{_selectedClips.Count} Items Selected.\n";
        }
        catch (Exception ex)
        {
            ProcessingLog += $"Error loading collection: {ex.Message}\n";
        }
    }

    private void GenerateDefaultFilePath()
    {
        try
        {
            // Get export directory from configuration
            var exportDir = _configurationService.Configuration.Export.GetResolvedExportDirectory();

            if (string.IsNullOrEmpty(exportDir))
                exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClipMateExport");

            // Ensure directory exists
            Directory.CreateDirectory(exportDir);

            // Generate filename: ClipMate_Export_MACHINENAME_CollectionName_YYYY-MM-DD_HHMMSS.XML
            var machineName = Environment.MachineName;
            var sanitizedCollectionName = SanitizeFileName(CollectionName);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var fileName = $"ClipMate_Export_{machineName}_{sanitizedCollectionName}_{timestamp}.XML";

            XmlExportFilePath = Path.Combine(exportDir, fileName);
        }
        catch (Exception ex)
        {
            ProcessingLog += $"Error generating default path: {ex.Message}\n";
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Clips";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(p => !invalidChars.Contains(p)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized)
            ? "Clips"
            : sanitized;
    }

    public void SetXmlExportFilePath(string filePath) => XmlExportFilePath = filePath;

    private bool CanExecuteXmlExport() => !IsProcessing && !IsComplete;

    [RelayCommand(CanExecute = nameof(CanExecuteXmlExport))]
    private async Task ExecuteXmlExport()
    {
        if (string.IsNullOrEmpty(XmlExportFilePath))
        {
            ProcessingLog += "Error: XML export file path not specified.\n";
            return;
        }

        if (_selectedClips.Count == 0)
        {
            ProcessingLog += "Error: No clips to export.\n";
            return;
        }

        IsProcessing = true;
        IsComplete = false;
        ProgressValue = 0;
        ProgressMaximum = _selectedClips.Count;
        ProcessingLog += "Starting XML export...\n";

        // Create cancellation token source for this export operation
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var collections = new List<Collection>();
            if (XmlExportIncludeCollections && _collectionId != null)
            {
                var collection = await _collectionService.GetByIdAsync(_collectionId.Value);
                if (collection != null)
                    collections.Add(collection);
            }

            void ProgressCallback(ExportProgressMessage msg)
            {
                ProcessingLog += $"{msg.Message}\n";
                ProgressValue = msg.ProcessedCount;
                if (!msg.IsComplete)
                    return;

                IsComplete = true;
                IsProcessing = false;
            }

            await _exportImportService.ExportToXmlAsync(
                _selectedClips,
                collections,
                XmlExportFilePath,
                ProgressCallback,
                _cancellationTokenSource.Token);

            // Update last export date and save directory
            _configurationService.Configuration.Export.DateLastExport = DateTime.Now;
            _configurationService.Configuration.Export.ExportDirectory = Path.GetDirectoryName(XmlExportFilePath);
            await _configurationService.SaveAsync();

            IsComplete = true;
        }
        catch (OperationCanceledException)
        {
            ProcessingLog += "Export cancelled by user.\n";
        }
        catch (Exception ex)
        {
            ProcessingLog += $"XML export failed: {ex.Message}\n";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Aborts the export operation if running, or closes the dialog if not.
    /// </summary>
    [RelayCommand]
    private void Abort()
    {
        if (IsProcessing)
        {
            // Cancel the running export operation
            _cancellationTokenSource?.Cancel();
            ProcessingLog += "Cancelling export...\n";
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
