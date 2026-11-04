using System.Diagnostics;
using System.IO;
using System.Windows;
using ClipMate.UITests.Fixtures;

namespace ClipMate.UITests.DocsScreenshots;

/// <summary>
/// The user docs' screenshots: this class is the shot list. Run by <c>build/build.ps1 -Target Docs-Screenshots</c>, not
/// by a plain UI-Test run. Each shot starts isolated copies of ClipMate on the sample database and saves a PNG next to
/// its page; what can't be reached from here is listed in docs/SCREENSHOTS_BY_HAND.md.
/// <para>
/// The script takes over the mouse and foreground, and writes nothing to the clipboard: it selects no clip row and
/// copies nothing. A clipboard change during a shot, by anyone, fails that shot, since a copy would have captured it.
/// </para>
/// </summary>
[Explicit]
[NotInParallel(SystemClipboard.ParallelKey)]
public class DocsScreenshotTests
{
    /// <summary>
    /// The folder the pictures go under (docs/docs).
    /// </summary>
    public const string OutputVariable = "CLIPMATE_DOCS_OUTPUT";

    /// <summary>
    /// The ClipMate.App.exe to run; networking copies need its exact path to have a firewall rule.
    /// </summary>
    public const string ExeVariable = "CLIPMATE_DOCS_EXE";

    private const string _desktopName = "Docs Desktop";
    private const string _laptopName = "Docs Laptop";
    private const string _firstClipTitle = "Fall farmers market hours";

    // The Explorer window hosts a text editor that fills in a moment after it reports the clip loaded
    private const int _webContentMatches = 10;

    private static readonly TimeSpan _discoveryTimeout = TimeSpan.FromSeconds(45);

    private DataObject _clipboardBackup = null!;
    private uint _clipboardSequence;

    private static string OutputFolder => Required(OutputVariable);

    private static string ExePath => Required(ExeVariable);

    [Before(Test)]
    public void RememberClipboard()
    {
        WindowCapture.UsePhysicalPixels();
        _clipboardBackup = SystemClipboard.Backup();
        _clipboardSequence = SystemClipboard.SequenceNumber;
    }

    [After(Test)]
    public void CheckClipboard()
    {
        if (SystemClipboard.SequenceNumber == _clipboardSequence)
            return;

        // A restore is itself a write, so it is only done when something already changed the clipboard
        SystemClipboard.Restore(_clipboardBackup);
        throw new InvalidOperationException("The clipboard changed during this shot. Nothing in the script writes to it, so check that nothing was copied while it ran, then run it again.");
    }

    [Test]
    public async Task ExplorerWindow()
    {
        await using var copy = await StartAsync();
        var page = new DocsCopyPage(copy.Process);

        await page.WaitForWindowTitleAsync(DocsCopyPage.ExplorerTitle);
        await page.WaitForClipRowAsync(DocsCopyPage.ExplorerTitle, _firstClipTitle);
        await Poll.UntilAsync(() => copy.ReadLog().Contains("Default tab selected and load complete", StringComparison.Ordinal), "the clip viewer to load");
        await SaveAsync(page, DocsCopyPage.ExplorerTitle, "user-interface/img/clipmate-explorer-overview.png", _webContentMatches);
    }

    [Test]
    public async Task ClassicWindow()
    {
        await using var copy = await StartAsync(
            "preferences.initial_show_mode=\"Classic\"",
            "preferences.classic_window_taskbar.left=100",
            "preferences.classic_window_taskbar.top=100",
            "preferences.classic_window_taskbar.width=900",
            "preferences.classic_window_taskbar.height=420");

        var page = new DocsCopyPage(copy.Process);

        await page.WaitForWindowTitleAsync(DocsCopyPage.ClassicTitle);
        await page.WaitForClipRowAsync(DocsCopyPage.ClassicTitle, _firstClipTitle);
        await SaveAsync(page, DocsCopyPage.ClassicTitle, "user-interface/img/clipmate-classic-overview.png", _webContentMatches);
    }

    [Test]
    public async Task AppearanceOptions()
    {
        await using var copy = await StartAsync();
        var page = new DocsCopyPage(copy.Process);

        await page.OpenOptionsAsync();
        await page.SelectOptionsTabAsync("Appearance");
        await SaveAsync(page, DocsCopyPage.OptionsTitle, "options/img/options-appearance.png");
    }

    [Test]
    public async Task AppearanceOptionsDark()
    {
        await using var copy = await StartAsync("appearance.app_theme=\"Dark\"");
        var page = new DocsCopyPage(copy.Process);

        await page.OpenOptionsAsync();
        await page.SelectOptionsTabAsync("Appearance");
        await SaveAsync(page, DocsCopyPage.OptionsTitle, "options/img/options-appearance-dark.png");
    }

    /// <summary>
    /// With networking off, so the tab lists no Discovered Devices. The Device Name is seeded because it defaults to the computer's name.
    /// </summary>
    [Test]
    public async Task DevicesTab()
    {
        await using var copy = await StartAsync($"networking.device_name=\"{_laptopName}\"");
        var page = new DocsCopyPage(copy.Process);

        await page.OpenOptionsAsync();
        await page.SelectOptionsTabAsync("Devices");
        await SaveAsync(page, DocsCopyPage.OptionsTitle, "devices/img/devices-tab.png");
    }

    /// <summary>
    /// Two copies pair with each other. The pictures are taken before anyone accepts; pairing is then finished, because a
    /// Pairing shot of two copies that can't pair would document something that doesn't work. Real Devices on the network
    /// are listed too, so no picture is taken of Discovered Devices, and only the exact seeded name is ever paired with.
    /// UI Automation can't read the Verification Code (a plain text block), so check by eye that the two pictures show the same one.
    /// </summary>
    [Test]
    public async Task PairingDialogs()
    {
        await FirewallRule.RequireForAsync(ExePath);

        await using var desktop = await StartAsync(NetworkingSettings(47911, _desktopName));
        await using var laptop = await StartCopyAsync(async copy =>
        {
            await UseSampleDatabaseAsync(copy);
            await ShareDatabaseAsync(copy);
        }, NetworkingSettings(47912, _laptopName));

        var requesting = new DocsCopyPage(desktop.Process);
        var answering = new DocsCopyPage(laptop.Process);

        var clock = Stopwatch.StartNew();
        await WaitForDiscoveryAsync(desktop, _laptopName);
        Report($"{_desktopName} discovered {_laptopName} after {clock.Elapsed.TotalSeconds:F1}s");
        await WaitForDiscoveryAsync(laptop, _desktopName);
        Report($"{_laptopName} discovered {_desktopName} after {clock.Elapsed.TotalSeconds:F1}s");

        await requesting.OpenDevicesAsync(_laptopName);
        await answering.OpenDevicesAsync(_desktopName);
        Report("Each lists the other under Discovered Devices");

        await requesting.PairWithAsync(_laptopName);
        clock.Restart();
        await SaveAsync(requesting, DocsCopyPage.PairingTitle, "devices/img/pairing-request.png");

        // Not brought to the foreground: it is only on screen until the approval dialog replaces it, and clicking it opens that dialog
        var notification = await answering.WaitForPairingNotificationAsync($"{_desktopName} wants to pair with this Device.");
        var notificationSize = await WindowCapture.SaveWhenSettledAsync(notification, Path.Combine(OutputFolder, "devices/img/pairing-notification.png"));
        Report($"devices/img/pairing-notification.png: {notificationSize.Width}x{notificationSize.Height}");

        await answering.WaitForApprovalDialogAsync();
        Report($"The approval dialog opened {clock.Elapsed.TotalSeconds:F1}s after the request");
        await Foreground.ActivateAsync(answering.Window(DocsCopyPage.PairingTitle), "the answering Pairing dialog");
        await SaveAsync(answering, DocsCopyPage.PairingTitle, "devices/img/pairing-approval.png");

        clock.Restart();
        await answering.AcceptPairingAsync();
        await requesting.AcceptPairingAsync();
        await WaitForSavedPeerAsync(desktop, _laptopName);
        await WaitForSavedPeerAsync(laptop, _desktopName);
        Report($"Pairing completed {clock.Elapsed.TotalSeconds:F1}s after the first Accept");

        await requesting.CloseDialogAsync(DocsCopyPage.PairingTitle, "Close");
        await requesting.WaitForPeerAsync(_laptopName);
        await answering.WaitForPeerAsync(_desktopName);
        Report("Each lists the other under Peers");

        // UI Automation doesn't report the Remote Database's tree row, so the sharing Device's log says when it was asked for its tree
        await requesting.CloseDialogAsync(DocsCopyPage.OptionsTitle, "Cancel");
        await Poll.UntilAsync(() => laptop.ReadLog().Contains("SharedDatabaseAccess/GetCollectionTree", StringComparison.Ordinal), "the Remote Database's collection tree to be read", _discoveryTimeout);
        requesting.MakeExplorerTaller();
        await SaveAsync(requesting, DocsCopyPage.ExplorerTitle, "devices/img/remote-database.png", _webContentMatches);
    }

    /// <summary>
    /// Turns on Share with Peers for the copy's one database. Its settings key is generated, so it can't be named as a setting.
    /// </summary>
    private static async Task ShareDatabaseAsync(AppFixture copy)
    {
        var path = Path.Combine(copy.DataDirectory, "clipmate.toml");
        var lines = (await File.ReadAllLinesAsync(path)).Where(p => !p.StartsWith("share_with_peers =", StringComparison.Ordinal)).ToList();
        lines.Insert(lines.FindIndex(p => p.StartsWith("[databases.", StringComparison.Ordinal)) + 1, "share_with_peers = true");
        await File.WriteAllLinesAsync(path, lines);
    }

    private static Task WaitForDiscoveryAsync(AppFixture copy, string deviceName) => Poll.UntilAsync(
        () => copy.ReadLog().Contains($"Discovered Device {deviceName} (", StringComparison.Ordinal), $"{deviceName} to be discovered", _discoveryTimeout);

    /// <summary>
    /// The settings file names a Peer once Pairing has completed on that copy.
    /// </summary>
    private static Task WaitForSavedPeerAsync(AppFixture copy, string deviceName) => Poll.UntilAsync(() =>
    {
        using var stream = new FileStream(Path.Combine(copy.DataDirectory, "clipmate.toml"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Contains(deviceName, StringComparison.Ordinal);
    }, $"{deviceName} to be saved as a Peer");

    private static string[] NetworkingSettings(int port, string deviceName) =>
    [
        "networking.enabled=true",
        $"networking.port={port}",
        $"networking.device_name=\"{deviceName}\"",
    ];

    /// <summary>
    /// Starts a copy on the sample database. The settings keep the picture free of the machine it runs on: without
    /// auto-targeting the QuickPaste toolbar shows "No target", not the title of the user's foreground window.
    /// </summary>
    private static Task<AppFixture> StartAsync(params string[] settings) => StartCopyAsync(UseSampleDatabaseAsync, settings);

    private static async Task<AppFixture> StartCopyAsync(Func<AppFixture, Task> afterFirstRun, params string[] settings)
    {
        var copy = new AppFixture();
        try
        {
            await copy.StartAppAsync(new AppStartOptions
            {
                ExePath = ExePath,
                SkipStartupCapture = true,
                WaitUntilCapturing = false,
                Settings = ["preferences.backup_interval_days=0", "preferences.quick_paste_auto_targeting_enabled=false", .. settings],
                AfterFirstRun = afterFirstRun,
            });

            return copy;
        }
        catch
        {
            await copy.DisposeAsync();
            throw;
        }
    }

    private static async Task UseSampleDatabaseAsync(AppFixture copy)
    {
        var sample = Path.Combine(AppContext.BaseDirectory, "DocsScreenshots", "SampleData", "clipmate.db");
        await Poll.UntilAsync(() =>
        {
            try
            {
                foreach (var item in new[] { "", "-wal", "-shm" })
                    File.Delete(copy.DatabasePath + item);

                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }, "the first run's database to be released");

        File.Copy(sample, copy.DatabasePath);
    }

    private static async Task SaveAsync(DocsCopyPage page, string title, string relativePath, int matches = 2)
    {
        var window = page.Window(title);
        await Foreground.ActivateAsync(window, $"the \"{title}\" window");
        var size = await WindowCapture.SaveWhenSettledAsync(window, Path.Combine(OutputFolder, relativePath), matches);
        Report($"{relativePath}: {size.Width}x{size.Height} at {WindowCapture.DpiOf(window)} DPI");
    }

    /// <summary>
    /// Writes to the process's real output: the test runner only shows what Console.Out gets from a test that fails.
    /// </summary>
    private static void Report(string message)
    {
        using var output = new StreamWriter(Console.OpenStandardOutput());
        output.WriteLine($"docs-screenshots: {message}");
    }

    private static string Required(string variable) => Environment.GetEnvironmentVariable(variable)
                                                       ?? throw new InvalidOperationException($"{variable} is not set: run this through build/build.ps1 -Target Docs-Screenshots");
}
