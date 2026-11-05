using System.Text.RegularExpressions;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Text Cleanup Options dialog. The dialog starts with the choices made the last time.
/// </summary>
public partial class TextCleanupDialogViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<TextCleanupDialogViewModel> _logger;

    public TextCleanupDialogViewModel(IConfigurationService configurationService, ILogger<TextCleanupDialogViewModel> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = _configurationService.Configuration.TextCleanup.Clone();
    }

    /// <summary>
    /// Gets the choices being edited.
    /// </summary>
    public TextCleanupConfiguration Options { get; }

    /// <summary>
    /// Gets a description of what is wrong with the choices, or null when they can be used.
    /// </summary>
    public string? Validate()
    {
        if (!Options.RegExFind)
            return null;

        try
        {
            _ = new Regex(Options.FindText);
            return null;
        }
        catch (ArgumentException ex)
        {
            return $"The Find What text isn't a valid regular expression: {ex.Message}";
        }
    }

    /// <summary>
    /// Keeps the choices for the next time.
    /// </summary>
    public async Task SaveAsync()
    {
        Options.ReBreakColumn = Math.Clamp(Options.ReBreakColumn, 1, 999);
        _configurationService.Configuration.TextCleanup = Options.Clone();

        try
        {
            await _configurationService.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save the Text Cleanup options");
        }
    }
}
