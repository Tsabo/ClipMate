using ClipMate.Core.Models;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for ClipViewerWindow - manages clip viewing state and operations.
/// </summary>
public partial class ClipViewerViewModel : ObservableObject
{
    #region Constructor

    public ClipViewerViewModel(IDatabaseManager databaseManager,
        ILogger<ClipViewerViewModel> logger)
    {
        _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Fields

    private readonly IDatabaseManager _databaseManager;
    private readonly ILogger<ClipViewerViewModel> _logger;
    private string? _currentDatabaseKey;

    #endregion

    #region Observable Properties

    /// <summary>
    /// The clip currently being viewed.
    /// </summary>
    [ObservableProperty]
    private Clip? _currentClip;

    /// <summary>
    /// The ID of the clip to load.
    /// </summary>
    [ObservableProperty]
    private Guid? _clipId;

    /// <summary>
    /// Indicates if data is being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Title for the window (derived from clip data).
    /// </summary>
    [ObservableProperty]
    private string _windowTitle = "Clip Viewer";

    #endregion

    #region Commands

    /// <summary>
    /// Loads a clip by ID from the specified database.
    /// </summary>
    /// <param name="args">Tuple of (clipId, databaseKey).</param>
    [RelayCommand]
    private async Task LoadClipAsync((Guid ClipId, string? DatabaseKey) args)
    {
        ClipId = args.ClipId;
        _currentDatabaseKey = args.DatabaseKey;
        IsLoading = true;

        try
        {
            Clip? clip = null;

            // Try to load from specified database first
            if (!string.IsNullOrEmpty(_currentDatabaseKey))
            {
                await using var context = _databaseManager.CreateDatabaseContext(_currentDatabaseKey);
                if (context != null)
                    clip = await context.Clips.FindAsync(args.ClipId);
            }

            // If no database key provided, try all loaded databases
            if (clip == null)
            {
                foreach (var (dbKey, context) in _databaseManager.CreateAllDatabaseContexts())
                {
                    await using (context)
                    {
                        clip = await context.Clips.FindAsync(args.ClipId);
                        if (clip == null)
                            continue;

                        _currentDatabaseKey = dbKey;
                        break;
                    }
                }
            }

            CurrentClip = clip;

            if (CurrentClip != null)
            {
                // Update window title with clip info
                var timestamp = CurrentClip.CapturedAt.ToString("g"); // General short date/time
                WindowTitle = string.IsNullOrWhiteSpace(CurrentClip.Title)
                    ? $"Clip Viewer - {timestamp}"
                    : $"Clip Viewer - {CurrentClip.Title} ({timestamp})";
            }
            else
                WindowTitle = "Clip Viewer - Not Found";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading clip {ClipId} from database {DatabaseKey}", args.ClipId, _currentDatabaseKey);
            WindowTitle = "Clip Viewer - Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes the current clip data.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (ClipId.HasValue)
            await LoadClipAsync((ClipId.Value, _currentDatabaseKey));
    }

    #endregion
}
