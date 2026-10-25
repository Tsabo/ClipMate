using System.Diagnostics;
using System.IO;
using System.Windows;
using ClipMate.UITests.PageObjects;
using WpfPilot;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// Starts an isolated ClipMate for UI tests: the ClipMate.App.exe built beside the tests, run with --data-dir
/// pointing at a fresh temporary folder, so the user's settings, databases and running ClipMate are never touched.
/// WPF Pilot attaches only when a test asks for <see cref="App" />.
/// </summary>
public class AppFixture : IAsyncDisposable
{
    private static readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(90);

    private AppDriver? _appDriver;
    private Process? _appProcess;

    public AppDriver App => _appDriver ??= AppDriver.AttachTo(Process.Id);

    public Process Process => _appProcess ?? throw new InvalidOperationException("App not started. Call StartAppAsync first.");

    /// <summary>
    /// The isolated data folder (settings, Databases\, Logs\).
    /// </summary>
    public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), $"clipmate-uitest-{Guid.NewGuid():N}");

    /// <summary>
    /// The default database the isolated copy creates on first run.
    /// </summary>
    public string DatabasePath => Path.Combine(DataDirectory, "Databases", "clipmate.db");

    /// <summary>
    /// Starts ClipMate and waits until its log reports that startup finished and clipboard monitoring is running.
    /// </summary>
    /// <param name="settings">
    /// Settings to change first, as section.key=value with the value written as TOML: preferences.default_editor_view="Markdown".
    /// </param>
    public async Task StartAppAsync(params string[] settings)
    {
        if (_appProcess != null)
            return;

        Directory.CreateDirectory(DataDirectory);
        if (settings.Length > 0)
            await ApplySettingsAsync(settings);

        _appProcess = StartProcess();

        // The app is a separate process, so its startup can only be observed by polling its log until a deadline
        try
        {
            await Poll.UntilAsync(() =>
            {
                if (_appProcess.HasExited)
                    throw new InvalidOperationException($"ClipMate exited during startup (exit code {_appProcess.ExitCode}). Logs: {LogDirectory}");

                // Clipboard monitoring starts after the initialization pipeline; a copy made in between is never captured
                var log = ReadLog();
                return log.Contains("Initialization pipeline completed", StringComparison.Ordinal) &&
                       log.Contains("Clipboard coordinator started successfully", StringComparison.Ordinal);
            }, "ClipMate's startup", _startupTimeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"ClipMate did not finish starting within {_startupTimeout.TotalSeconds}s. Logs: {LogDirectory}");
        }

        await WaitUntilCapturingAsync();
    }

    private Process StartProcess()
    {
        var appPath = Path.Combine(AppContext.BaseDirectory, "ClipMate.App.exe");
        if (!File.Exists(appPath))
            throw new FileNotFoundException($"ClipMate.App.exe not found beside the tests at {appPath}. Build ClipMate.UITests first.");

        // The app also logs to its console; discard that copy (the log files are read below) so test output stays readable
        var process = Process.Start(new ProcessStartInfo(appPath, ["--data-dir", DataDirectory])
                      {
                          UseShellExecute = false,
                          RedirectStandardOutput = true,
                          RedirectStandardError = true,
                      })
                      ?? throw new InvalidOperationException("Failed to start ClipMate process");

        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    /// <summary>
    /// ClipMate rejects a settings file without its databases, so it is started once to write its defaults, which are
    /// then changed. That run's log is removed, so startup is only ever read from the run under test.
    /// </summary>
    private async Task ApplySettingsAsync(string[] settings)
    {
        using (var first = StartProcess())
        {
            try
            {
                await Poll.UntilAsync(() => ReadLog().Contains("Initialization pipeline completed", StringComparison.Ordinal),
                    "ClipMate's first run", _startupTimeout);
            }
            finally
            {
                // The tree includes the WebView2 processes
                first.Kill(true);
                await first.WaitForExitAsync();
            }
        }

        var settingsPath = Path.Combine(DataDirectory, "clipmate.toml");
        var lines = (await File.ReadAllLinesAsync(settingsPath)).ToList();
        foreach (var setting in settings)
        {
            var equals = setting.IndexOf('=');
            var dot = equals < 0 ? -1 : setting.LastIndexOf('.', equals);
            if (dot < 0)
                throw new ArgumentException($"Setting '{setting}' is not section.key=value", nameof(settings));

            var header = $"[{setting[..dot]}]";
            var key = setting[(dot + 1)..equals];
            var start = lines.IndexOf(header);
            if (start < 0)
                throw new ArgumentException($"ClipMate's settings have no section {header}", nameof(settings));

            var index = start + 1;
            while (index < lines.Count && !lines[index].StartsWith('[') && !lines[index].StartsWith($"{key} =", StringComparison.Ordinal))
                index++;

            var line = $"{key} = {setting[(equals + 1)..]}";
            if (index < lines.Count && !lines[index].StartsWith('['))
                lines[index] = line;
            else
                lines.Insert(start + 1, line);
        }

        await File.WriteAllLinesAsync(settingsPath, lines);
        await Poll.UntilAsync(() => TryDelete(LogDirectory), "the first run's log to be deleted", TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// The log line that monitoring has started comes slightly before the clip list is ready for new clips. A marker
    /// that shows up in the list proves that every later copy is captured and listed.
    /// </summary>
    private async Task WaitUntilCapturingAsync()
    {
        var marker = $"uitest-ready-{Guid.NewGuid():N}";
        var clipList = new ClipListPage(Process);
        var attempt = 0;
        await Poll.UntilAsync(() =>
        {
            if (attempt++ % 10 == 0)
                SystemClipboard.OnSta(() => Clipboard.SetText(marker));

            return clipList.FindRow(marker);
        }, "the startup marker in the clip list", _startupTimeout);
    }

    /// <summary>
    /// Gets the main application window element.
    /// </summary>
    public Element GetMainWindow() => App.GetElement(x => x.TypeName == "MainWindow");

    private string LogDirectory => Path.Combine(DataDirectory, "Logs");

    /// <summary>
    /// Everything the isolated ClipMate has logged so far.
    /// </summary>
    public string ReadLog()
    {
        if (!Directory.Exists(LogDirectory))
            return string.Empty;

        // The early and configured loggers roll to separate files, so read them all; the app still has them open
        return string.Concat(Directory.EnumerateFiles(LogDirectory, "clipmate-*.log").Select(p =>
        {
            using var stream = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }));
    }

    public async ValueTask DisposeAsync()
    {
        _appDriver?.Dispose();
        _appDriver = null;

        if (_appProcess != null)
        {
            if (!_appProcess.HasExited)
            {
                // The tree includes the WebView2 processes, which otherwise keep the data folder locked
                _appProcess.Kill(true);
                await _appProcess.WaitForExitAsync();
            }

            _appProcess.Dispose();
            _appProcess = null;
        }

        // Killed child processes release their files a moment after the app exits; a folder still locked after that is left
        try
        {
            await Poll.UntilAsync(() => TryDelete(DataDirectory), "the test data folder to be deleted", TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
        }

        GC.SuppressFinalize(this);
    }

    private static bool TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
