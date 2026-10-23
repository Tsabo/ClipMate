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
    public async Task StartAppAsync()
    {
        if (_appProcess != null)
            return;

        Directory.CreateDirectory(DataDirectory);
        var appPath = Path.Combine(AppContext.BaseDirectory, "ClipMate.App.exe");
        if (!File.Exists(appPath))
            throw new FileNotFoundException($"ClipMate.App.exe not found beside the tests at {appPath}. Build ClipMate.UITests first.");

        // The app also logs to its console; discard that copy (the log files are read below) so test output stays readable
        _appProcess = Process.Start(new ProcessStartInfo(appPath, ["--data-dir", DataDirectory])
                      {
                          UseShellExecute = false,
                          RedirectStandardOutput = true,
                          RedirectStandardError = true,
                      })
                      ?? throw new InvalidOperationException("Failed to start ClipMate process");

        _appProcess.OutputDataReceived += (_, _) => { };
        _appProcess.ErrorDataReceived += (_, _) => { };
        _appProcess.BeginOutputReadLine();
        _appProcess.BeginErrorReadLine();

        // The app is a separate process, so its startup can only be observed by polling its log until a deadline
        var deadline = DateTime.UtcNow + _startupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_appProcess.HasExited)
                throw new InvalidOperationException($"ClipMate exited during startup (exit code {_appProcess.ExitCode}). Logs: {LogDirectory}");

            // Clipboard monitoring starts after the initialization pipeline; a copy made in between is never captured
            var log = ReadLog();
            if (log.Contains("Initialization pipeline completed", StringComparison.Ordinal) &&
                log.Contains("Clipboard coordinator started successfully", StringComparison.Ordinal))
            {
                await WaitUntilCapturingAsync();
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"ClipMate did not finish starting within {_startupTimeout.TotalSeconds}s. Logs: {LogDirectory}");
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

        // Killed child processes release their files a moment after the app exits
        for (var attempt = 0; attempt < 20 && Directory.Exists(DataDirectory); attempt++)
        {
            try
            {
                Directory.Delete(DataDirectory, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(250);
            }
        }

        GC.SuppressFinalize(this);
    }
}
