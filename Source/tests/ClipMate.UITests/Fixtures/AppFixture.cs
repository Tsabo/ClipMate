using System.Diagnostics;
using System.IO;
using WpfPilot;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// Shared fixture for initializing and cleaning up the ClipMate application in UI tests.
/// Uses WPF Pilot to launch and manage the application instance.
/// </summary>
public class AppFixture : IAsyncDisposable
{
    private AppDriver? _appDriver;
    private Process? _appProcess;
    private string? _testDatabasePath;

    public AppDriver App => _appDriver ?? throw new InvalidOperationException("App not started. Call StartAppAsync first.");

    /// <summary>
    /// Starts the ClipMate application with a test database.
    /// Creates a temporary database for isolated testing.
    /// </summary>
    public async Task StartAppAsync()
    {
        if (_appDriver != null)
            return; // Already started

        // Create temporary test database
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"ClipMateTest_{Guid.NewGuid()}.cm5");
        
        // Get the ClipMate.App executable path (assuming it's in bin/Debug or bin/Release)
        var appPath = GetClipMateExecutablePath();

        // Launch the application with test database argument
        var startInfo = new ProcessStartInfo
        {
            FileName = appPath,
            Arguments = $"--database \"{_testDatabasePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _appProcess = Process.Start(startInfo);
        
        if (_appProcess == null)
            throw new InvalidOperationException("Failed to start ClipMate process");

        // Wait for main window to appear and initialize WPF Pilot
        await Task.Delay(2000); // Give app time to initialize
        
        _appDriver = AppDriver.AttachTo(_appProcess.Id);
    }

    /// <summary>
    /// Gets the main application window element.
    /// </summary>
    public Element GetMainWindow()
    {
        if (_appDriver == null)
            throw new InvalidOperationException("App not started");

        // Wait for and return the main window
        return _appDriver.GetElement(x => x.TypeName == "MainWindow");
    }

    private static string GetClipMateExecutablePath()
    {
        // Navigate from test project bin to app bin
        var testAssembly = typeof(AppFixture).Assembly.Location;
        var testBinDir = Path.GetDirectoryName(testAssembly)!;
        
        // Go up from tests/ClipMate.UITests/bin/Debug/net10.0-windows
        var sourceDir = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", "..", ".."));
        var appBinDir = Path.Combine(sourceDir, "src", "ClipMate.App", "bin", "Debug", "net10.0-windows");
        var appPath = Path.Combine(appBinDir, "ClipMate.App.exe");

        if (!File.Exists(appPath))
            throw new FileNotFoundException($"ClipMate.App.exe not found at: {appPath}. Build the solution first.");

        return appPath;
    }

    public async ValueTask DisposeAsync()
    {
        if (_appDriver != null)
        {
            _appDriver.Dispose();
            _appDriver = null;
        }

        if (_appProcess != null && !_appProcess.HasExited)
        {
            _appProcess.Kill();
            _appProcess.WaitForExit(5000);
            _appProcess.Dispose();
            _appProcess = null;
        }

        // Clean up test database
        if (_testDatabasePath != null && File.Exists(_testDatabasePath))
        {
            try
            {
                File.Delete(_testDatabasePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        GC.SuppressFinalize(this);
        await ValueTask.CompletedTask;
    }
}
