using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
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

    public ClipViewerViewModel(IClipRepository clipRepository, ILogger<ClipViewerViewModel> logger)
    {
        _clipRepository = clipRepository ?? throw new ArgumentNullException(nameof(clipRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Fields

    private readonly IClipRepository _clipRepository;
    private readonly ILogger<ClipViewerViewModel> _logger;

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
    /// Loads a clip by ID.
    /// </summary>
    [RelayCommand]
    private async Task LoadClipAsync(Guid clipId)
    {
        ClipId = clipId;
        IsLoading = true;

        try
        {
            CurrentClip = await _clipRepository.GetByIdAsync(clipId);

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
            _logger.LogError(ex, "Error loading clip {ClipId}", clipId);
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
            await LoadClipAsync(ClipId.Value);
    }

    #endregion
}
