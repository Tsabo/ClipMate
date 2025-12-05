using System.Collections.ObjectModel;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Application Profiles options tab.
/// </summary>
public partial class ApplicationProfilesOptionsViewModel : ObservableObject
{
    private readonly IApplicationProfileService? _applicationProfileService;
    private readonly ILogger<ApplicationProfilesOptionsViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ApplicationProfileNode> _applicationProfileNodes = [];

    [ObservableProperty]
    private bool _enableApplicationProfiles;

    public ApplicationProfilesOptionsViewModel(
        ILogger<ApplicationProfilesOptionsViewModel> logger,
        IApplicationProfileService? applicationProfileService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _applicationProfileService = applicationProfileService;
    }

    /// <summary>
    /// Loads application profiles configuration.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_applicationProfileService != null)
        {
            EnableApplicationProfiles = _applicationProfileService.IsApplicationProfilesEnabled();
            await LoadApplicationProfilesAsync();
        }

        _logger.LogDebug("Application profiles configuration loaded");
    }

    /// <summary>
    /// Saves application profiles configuration.
    /// </summary>
    public async Task SaveAsync()
    {
        if (_applicationProfileService != null)
        {
            _applicationProfileService.SetApplicationProfilesEnabled(EnableApplicationProfiles);

            // Save updated profile states back to storage
            foreach (var profileNode in ApplicationProfileNodes)
                await _applicationProfileService.UpdateProfileAsync(profileNode.Profile);
        }

        _logger.LogDebug("Application profiles configuration saved");
    }

    /// <summary>
    /// Loads all application profiles from the service.
    /// </summary>
    [RelayCommand]
    private async Task LoadApplicationProfilesAsync()
    {
        if (_applicationProfileService == null)
        {
            _logger.LogWarning("Application profile service not available");
            return;
        }

        try
        {
            var profiles = await _applicationProfileService.GetAllProfilesAsync();
            ApplicationProfileNodes.Clear();

            foreach (var kvp in profiles.OrderBy(p => p.Key))
            {
                var profileNode = new ApplicationProfileNode(kvp.Value);
                ApplicationProfileNodes.Add(profileNode);
            }

            _logger.LogInformation("Loaded {Count} application profiles", ApplicationProfileNodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load application profiles");
        }
    }

    /// <summary>
    /// Deletes all application profiles.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAllProfilesAsync()
    {
        if (_applicationProfileService == null)
        {
            _logger.LogWarning("Application profile service not available");
            return;
        }

        try
        {
            await _applicationProfileService.DeleteAllProfilesAsync();
            ApplicationProfileNodes.Clear();
            _logger.LogInformation("Deleted all application profiles");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete application profiles");
        }
    }
}
