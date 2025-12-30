using System.Diagnostics;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Export;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

public partial class FlatFileExportViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly IExportImportService _exportImportService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private bool _eraseDirectoryContents;

    [ObservableProperty]
    private string _flatFileExportDirectory = string.Empty;

    [ObservableProperty]
    private bool _isBmpSelected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DoneCommand))]
    private bool _isComplete;

    [ObservableProperty]
    private bool _isJpgSelected;

    [ObservableProperty]
    private bool _isPngSelected = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteFlatFileExportCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private int _jpegQualitySlider = 85;

    [ObservableProperty]
    private bool _openFolderWhenFinished = true;

    [ObservableProperty]
    private string _processingLog = string.Empty;

    [ObservableProperty]
    private int _progressMaximum = 100;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private bool _resetSequence;

    private List<Clip> _selectedClips = [];

    [ObservableProperty]
    private FileNamingStrategy _selectedNamingStrategy = FileNamingStrategy.Sequential;

    public FlatFileExportViewModel(IExportImportService exportImportService,
        IConfigurationService configurationService)
    {
        _exportImportService = exportImportService;
        _configurationService = configurationService;
    }

    public bool? DialogResult { get; set; }

    /// <summary>
    /// Gets the count of selected clips for display.
    /// </summary>
    public int SelectedClipCount => _selectedClips.Count;

    /// <summary>
    /// Initializes the ViewModel with the clips to export.
    /// </summary>
    /// <param name="selectedClips">The clips to export.</param>
    public void Initialize(IEnumerable<Clip> selectedClips)
    {
        _selectedClips = selectedClips.ToList();
        ProcessingLog = $"{_selectedClips.Count} clip(s) selected for export.\n";
    }

    public Task InitializeAsync()
    {
        try
        {
            var export = _configurationService.Configuration.Export;

            // Load export directory using helper method
            FlatFileExportDirectory = export.GetResolvedExportDirectory();

            // Load all export settings
            EraseDirectoryContents = export.EraseDirectoryContents;
            OpenFolderWhenFinished = export.OpenFolderWhenFinished;
            ResetSequence = export.ResetSequence;
            SelectedNamingStrategy = export.FileNamingStrategy;
            JpegQualitySlider = export.JpegQuality;

            // Set image format radio buttons
            switch (export.ImageFormat)
            {
                case ImageExportFormat.Jpg:
                    IsJpgSelected = true;
                    IsPngSelected = false;
                    IsBmpSelected = false;
                    break;
                case ImageExportFormat.Bmp:
                    IsBmpSelected = true;
                    IsPngSelected = false;
                    IsJpgSelected = false;
                    break;
                default: // PNG
                    IsPngSelected = true;
                    IsJpgSelected = false;
                    IsBmpSelected = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            ProcessingLog += $"Init error: {ex.Message}\n";
        }

        return Task.CompletedTask;
    }

    public void SetFlatFileExportDirectory(string directory)
    {
        FlatFileExportDirectory = directory;
        if (!string.IsNullOrEmpty(directory))
            _configurationService.Configuration.Export.ExportDirectory = directory;
    }

    private async Task SaveSettingsAsync()
    {
        var export = _configurationService.Configuration.Export;

        export.ExportDirectory = FlatFileExportDirectory;
        export.EraseDirectoryContents = EraseDirectoryContents;
        export.OpenFolderWhenFinished = OpenFolderWhenFinished;
        export.ResetSequence = ResetSequence;
        export.FileNamingStrategy = SelectedNamingStrategy;
        export.JpegQuality = JpegQualitySlider;

        // Save image format
        if (IsJpgSelected)
            export.ImageFormat = ImageExportFormat.Jpg;
        else if (IsBmpSelected)
            export.ImageFormat = ImageExportFormat.Bmp;
        else
            export.ImageFormat = ImageExportFormat.Png;

        export.DateLastExport = DateTime.Now;

        // Persist configuration
        await _configurationService.SaveAsync();
    }

    private bool CanExecuteFlatFileExport() => !IsProcessing && !IsComplete;

    [RelayCommand(CanExecute = nameof(CanExecuteFlatFileExport))]
    private async Task ExecuteFlatFileExport()
    {
        if (string.IsNullOrEmpty(FlatFileExportDirectory))
        {
            ProcessingLog += "Error: Export directory not selected.\n";
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
        ProcessingLog += "Starting flat-file export...\n";

        // Create cancellation token source for this export operation
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // Get current sequence number (reset if requested)
            var export = _configurationService.Configuration.Export;
            var startSequence = ResetSequence
                ? 1
                : export.Sequence;

            void ProgressCallback(ExportProgressMessage msg)
            {
                ProcessingLog += $"{msg.Message}\n";
                ProgressValue = msg.ProcessedCount;

                if (!msg.IsComplete)
                    return;

                IsComplete = true;
                IsProcessing = false;
            }

            // Determine selected image format
            var imageFormat = IsJpgSelected
                ? ImageExportFormat.Jpg
                : IsBmpSelected
                    ? ImageExportFormat.Bmp
                    : ImageExportFormat.Png;

            var finalSequence = await _exportImportService.ExportClipsToFilesAsync(
                _selectedClips,
                FlatFileExportDirectory,
                SelectedNamingStrategy,
                ResetSequence,
                JpegQualitySlider,
                ProgressCallback,
                startSequence,
                imageFormat,
                _cancellationTokenSource.Token);

            // Update sequence number for next export
            export.Sequence = finalSequence;

            // Save all settings
            await SaveSettingsAsync();

            // Open folder if requested
            if (OpenFolderWhenFinished)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = FlatFileExportDirectory,
                        UseShellExecute = true,
                    });
                }
                catch
                {
                    // Ignore folder open errors
                }
            }

            ProcessingLog += "Export completed successfully.\n";
            IsComplete = true;
        }
        catch (OperationCanceledException)
        {
            ProcessingLog += "Export cancelled by user.\n";
        }
        catch (Exception ex)
        {
            ProcessingLog += $"Export failed: {ex.Message}\n";
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
