using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the About dialog.
/// </summary>
public partial class AboutDialogViewModel : ObservableObject
{
    private readonly ILogger<AboutDialogViewModel> _logger;
    private readonly IUpdateCheckService _updateCheckService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutDialogViewModel" /> class.
    /// </summary>
    public AboutDialogViewModel(IUpdateCheckService updateCheckService, ILogger<AboutDialogViewModel> logger)
    {
        _updateCheckService = updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get version from assembly (InformationalVersion includes semantic versioning like "1.0.0-alpha.3")
        var assembly = Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var semVer = infoVersion?.InformationalVersion ?? "0.0.0";

        // Display version with 'v' prefix
        Version = $"v{semVer}";

        // Store semantic version for update checks (without 'v' prefix)
        VersionNumber = semVer;

        // Get copyright year
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
        Copyright = copyright?.Copyright ?? $"Copyright © {DateTime.Now.Year} Jeremy Brown and ClipMate Contributors";

        // Initialize credits
        InitializeCredits();
    }

    /// <summary>
    /// Gets the application version string for display.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the version number for update checks (without 'v' prefix).
    /// </summary>
    public string VersionNumber { get; }

    /// <summary>
    /// Gets the copyright notice.
    /// </summary>
    public string Copyright { get; }

    /// <summary>
    /// Gets the collection of third-party credits.
    /// </summary>
    public ObservableCollection<CreditItem> Credits { get; } = [];

    /// <summary>
    /// Opens the GitHub repository in the default browser.
    /// </summary>
    [RelayCommand]
    private void ViewOnGitHub() => OpenUrl("https://github.com/Tsabo/ClipMate");

    /// <summary>
    /// Opens the GitHub issues page in the default browser.
    /// </summary>
    [RelayCommand]
    private void ReportIssue() => OpenUrl("https://github.com/Tsabo/ClipMate/issues");

    /// <summary>
    /// Opens the documentation site in the default browser.
    /// </summary>
    [RelayCommand]
    private void ViewDocumentation() => OpenUrl("https://jeremy.browns.info/ClipMate/");

    /// <summary>
    /// Checks for updates from GitHub releases.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdates()
    {
        try
        {
            _logger.LogInformation("Checking for updates manually from About dialog");

            var update = await _updateCheckService.CheckForUpdatesAsync(
                VersionNumber,
                false);

            if (update != null)
            {
                var result = DXMessageBox.Show(
                    $"A new version of ClipMate is available!\n\n" +
                    $"Current Version: {Version}\n" +
                    $"New Version: v{update.Version}\n" +
                    $"Released: {update.PublishedAt:MMMM d, yyyy}\n\n" +
                    $"Would you like to view the release notes and download?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    OpenUrl(update.ReleaseUrl);
            }
            else
            {
                DXMessageBox.Show(
                    $"You are running the latest version of ClipMate ({Version}).",
                    "No Updates Available",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");

            DXMessageBox.Show(
                "Unable to check for updates. Please check your internet connection and try again.\n\n" +
                "You can also manually check for updates at:\n" +
                "https://github.com/clipmate/ClipMate/releases",
                "Update Check Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Opens the localization contribution guide.
    /// </summary>
    [RelayCommand]
    private void ViewLocalizationInfo() => OpenUrl("https://github.com/Tsabo/ClipMate/blob/main/CONTRIBUTING.md#localization");

    /// <summary>
    /// Opens a URL in a credit item.
    /// </summary>
    [RelayCommand]
    private void OpenCreditUrl(string? url)
    {
        if (!string.IsNullOrEmpty(url))
            OpenUrl(url);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Silently fail if browser cannot be opened
        }
    }

    private void InitializeCredits()
    {
        // Original ClipMate attribution
        Credits.Add(new CreditItem
        {
            Name = "Original ClipMate",
            Description = "Based on the original ClipMate clipboard manager by Chris Thornton / Thornsoft Development, Inc.",
            License = "Inspiration",
            Url = "https://www.thornsoft.com/",
        });

        // Vendored Libraries
        Credits.Add(new CreditItem
        {
            Name = "Emoji.Wpf",
            Description = "Emoji rendering and custom color font support",
            License = "WTFPL",
            Url = "https://github.com/samhocevar/emoji.wpf",
        });

        Credits.Add(new CreditItem
        {
            Name = "Typography",
            Description = "OpenType font parsing and glyph rendering",
            License = "MIT",
            Url = "https://github.com/LayoutFarm/Typography",
        });

        Credits.Add(new CreditItem
        {
            Name = "WpfHexaEditor",
            Description = "Binary content viewing and hex editing",
            License = "Apache 2.0",
            Url = "https://github.com/abbaye/WpfHexEditorControl",
        });

        Credits.Add(new CreditItem
        {
            Name = "Icons8",
            Description = "Application icons",
            License = "Icons8 License",
            Url = "https://icons8.com",
        });

        // UI Frameworks
        Credits.Add(new CreditItem
        {
            Name = "CommunityToolkit.Mvvm",
            Description = "MVVM infrastructure, observable objects, messaging",
            License = "MIT",
            Url = "https://github.com/CommunityToolkit/dotnet",
        });

        Credits.Add(new CreditItem
        {
            Name = "DevExpress WPF",
            Description = "WPF UI controls, theming, data grids, ribbon, dialogs",
            License = "Commercial",
            Url = "https://www.devexpress.com/",
        });

        Credits.Add(new CreditItem
        {
            Name = "Monaco Editor",
            Description = "Code and text editing with syntax highlighting",
            License = "MIT",
            Url = "https://github.com/microsoft/monaco-editor",
        });

        Credits.Add(new CreditItem
        {
            Name = "Microsoft.Web.WebView2",
            Description = "HTML preview using Microsoft Edge WebView2",
            License = "Microsoft License",
            Url = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
        });

        // Data & Configuration
        Credits.Add(new CreditItem
        {
            Name = "Tomlyn",
            Description = "TOML configuration file parsing",
            License = "BSD-2-Clause",
            Url = "https://github.com/xoofx/Tomlyn",
        });

        Credits.Add(new CreditItem
        {
            Name = "Dapper",
            Description = "Micro-ORM for database operations",
            License = "Apache 2.0",
            Url = "https://github.com/DapperLib/Dapper",
        });

        // Logging & Audio
        Credits.Add(new CreditItem
        {
            Name = "Serilog",
            Description = "Structured logging framework",
            License = "Apache 2.0",
            Url = "https://github.com/serilog/serilog",
        });

        Credits.Add(new CreditItem
        {
            Name = "NAudio",
            Description = "Sound playback for system notifications",
            License = "MIT",
            Url = "https://github.com/naudio/NAudio",
        });

        // Build Tools
        Credits.Add(new CreditItem
        {
            Name = "Cake Build",
            Description = "Build automation and CI/CD pipeline orchestration",
            License = "MIT",
            Url = "https://github.com/cake-build/cake",
        });

        Credits.Add(new CreditItem
        {
            Name = "nanoemoji",
            Description = "Building color emoji fonts from SVG files",
            License = "Apache 2.0",
            Url = "https://github.com/googlefonts/nanoemoji",
        });

        Credits.Add(new CreditItem
        {
            Name = "fonttools",
            Description = "Font file manipulation and metadata application",
            License = "MIT",
            Url = "https://github.com/fonttools/fonttools",
        });

        // Testing
        Credits.Add(new CreditItem
        {
            Name = "TUnit",
            Description = "Modern test framework for unit tests",
            License = "MIT",
            Url = "https://github.com/thomhurst/TUnit",
        });

        Credits.Add(new CreditItem
        {
            Name = "Moq",
            Description = "Mocking framework for unit tests",
            License = "BSD-3-Clause",
            Url = "https://github.com/devlooped/moq",
        });

        // Microsoft Libraries
        Credits.Add(new CreditItem
        {
            Name = "Microsoft.Extensions.*",
            Description = "Dependency injection, logging, hosting abstractions",
            License = "MIT",
            Url = "https://github.com/dotnet/runtime",
        });

        Credits.Add(new CreditItem
        {
            Name = "Microsoft.EntityFrameworkCore.Sqlite",
            Description = "Entity Framework Core SQLite provider",
            License = "MIT",
            Url = "https://github.com/dotnet/efcore",
        });
    }
}

/// <summary>
/// Represents a third-party library credit entry.
/// </summary>
public class CreditItem
{
    /// <summary>
    /// Gets or sets the name of the library or component.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a description of what the library is used for.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the license type.
    /// </summary>
    public string License { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL to the library's repository or homepage.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
