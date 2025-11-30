using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit;

/// <summary>
/// Provides shared test data for application profile tests.
/// Based on ClipMate 7.5 registry export data.
/// </summary>
public static class ApplicationProfileTestFixtures
{
    /// <summary>
    /// Gets a test profile for Notepad (minimal text-only application).
    /// </summary>
    public static ApplicationProfile GetNotepadProfile() => new()
    {
        ApplicationName = "NOTEPAD",
        Enabled = true,
        Formats = new Dictionary<string, bool>
        {
            ["TEXT"] = true,
            ["CF_UNICODETEXT"] = false, // ClipMate 7.5 uses UNICODTEXT
            ["LOCALE"] = false,
        },
    };

    /// <summary>
    /// Gets a test profile for Chrome (browser with HTML and text).
    /// </summary>
    public static ApplicationProfile GetChromeProfile() => new()
    {
        ApplicationName = "CHROME",
        Enabled = true,
        Formats = new Dictionary<string, bool>
        {
            ["TEXT"] = true,
            ["HTML Format"] = true,
            ["CF_UNICODETEXT"] = false,
            ["BITMAP"] = false,
            ["DIB"] = false,
            ["Chromium internal source RFH token"] = false,
            ["Chromium internal source URL"] = false,
            ["LOCALE"] = false,
        },
    };

    /// <summary>
    /// Gets a test profile for Visual Studio (IDE with many formats).
    /// </summary>
    public static ApplicationProfile GetDevenvProfile() => new()
    {
        ApplicationName = "DEVENV",
        Enabled = true,
        Formats = new Dictionary<string, bool>
        {
            ["TEXT"] = true,
            ["Rich Text Format"] = true,
            ["CF_UNICODETEXT"] = false,
            ["VS Original Clipboard Text Format"] = false,
            ["Xaml"] = false,
            ["RoslynFormat-StringCopyPasteCommandHandlerV1"] = false,
            ["ApplicationTrust"] = false,
            ["DataObject"] = false,
            ["Ole Private Data"] = false,
            ["LOCALE"] = false,
        },
    };

    /// <summary>
    /// Gets a test profile for Windows Explorer (files and images).
    /// </summary>
    public static ApplicationProfile GetExplorerProfile() => new()
    {
        ApplicationName = "EXPLORER",
        Enabled = true,
        Formats = new Dictionary<string, bool>
        {
            ["TEXT"] = true,
            ["HDROP"] = true,
            ["BITMAP"] = true,
            ["HTML Format"] = true,
            ["Filename"] = true,
            ["AsyncFlag"] = false,
            ["DataObject"] = false,
            ["Shell IDList Array"] = false,
            ["Preferred DropEffect"] = false,
        },
    };

    /// <summary>
    /// Gets all test profiles.
    /// </summary>
    public static IEnumerable<ApplicationProfile> GetAllProfiles()
    {
        yield return GetNotepadProfile();
        yield return GetChromeProfile();
        yield return GetDevenvProfile();
        yield return GetExplorerProfile();
    }

    /// <summary>
    /// Gets a minimal profile with smart defaults for testing auto-generation.
    /// </summary>
    public static ApplicationProfile GetSmartDefaultProfile(string appName) => new()
    {
        ApplicationName = appName,
        Enabled = true,
        Formats = new Dictionary<string, bool>
        {
            ["TEXT"] = true,
            ["CF_UNICODETEXT"] = true,
            ["BITMAP"] = true,
            ["HDROP"] = true,
            ["HTML Format"] = true,
            ["Rich Text Format"] = false,
            ["DataObject"] = false,
            ["LOCALE"] = false,
            ["Ole Private Data"] = false,
        },
    };
}
