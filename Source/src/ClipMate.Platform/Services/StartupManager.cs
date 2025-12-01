using System.IO;
using System.Reflection;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for managing Windows startup configuration via the Run registry key.
/// </summary>
public class StartupManager : IStartupManager
{
    private const string _runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string _appName = "ClipMate";

    private readonly ILogger<StartupManager> _logger;

    public StartupManager(ILogger<StartupManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<(bool success, bool isEnabled, string? errorMessage)> IsEnabledAsync()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, false);
            if (key == null)
            {
                _logger.LogWarning("Unable to open Run registry key for reading");
                return Task.FromResult((false, false, (string?)"Unable to access Windows startup settings."));
            }

            var value = key.GetValue(_appName) as string;
            var isEnabled = !string.IsNullOrEmpty(value);

            _logger.LogDebug("Startup enabled check: {IsEnabled}", isEnabled);
            return Task.FromResult((true, isEnabled, (string?)null));
        }
        catch (SecurityException ex)
        {
            _logger.LogError(ex, "Security exception checking startup status");
            return Task.FromResult((false, false, (string?)"Access denied. You may not have permission to access startup settings."));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Unauthorized access checking startup status");
            return Task.FromResult((false, false, (string?)"Access denied. Administrator privileges may be required."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking startup status");
            return Task.FromResult((false, false, (string?)$"Unexpected error: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<(bool success, string? errorMessage)> EnableAsync()
    {
        try
        {
            // Get the executable path
            var executablePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;

            if (string.IsNullOrEmpty(executablePath))
            {
                _logger.LogError("Unable to determine executable path for startup configuration");
                return Task.FromResult((false, (string?)"Unable to determine application location."));
            }

            // Wrap path in quotes to handle spaces
            var commandLine = $"\"{executablePath}\"";

            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, true);
            if (key == null)
            {
                _logger.LogError("Unable to open Run registry key for writing");
                return Task.FromResult((false, (string?)"Unable to access Windows startup settings."));
            }

            key.SetValue(_appName, commandLine, RegistryValueKind.String);
            _logger.LogInformation("Startup enabled successfully: {Path}", commandLine);

            return Task.FromResult((true, (string?)null));
        }
        catch (SecurityException ex)
        {
            _logger.LogError(ex, "Security exception enabling startup");
            return Task.FromResult((false, (string?)"Access denied. You may not have permission to modify startup settings."));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Unauthorized access enabling startup");
            return Task.FromResult((false, (string?)"Access denied. Administrator privileges may be required."));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO exception enabling startup");
            return Task.FromResult((false, (string?)$"Registry error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error enabling startup");
            return Task.FromResult((false, (string?)$"Unexpected error: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<(bool success, string? errorMessage)> DisableAsync()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, true);
            if (key == null)
            {
                _logger.LogError("Unable to open Run registry key for writing");
                return Task.FromResult((false, (string?)"Unable to access Windows startup settings."));
            }

            // Check if value exists before trying to delete
            var value = key.GetValue(_appName);
            if (value != null)
            {
                key.DeleteValue(_appName, false);
                _logger.LogInformation("Startup disabled successfully");
            }
            else
                _logger.LogDebug("Startup was not enabled, nothing to disable");

            return Task.FromResult((true, (string?)null));
        }
        catch (SecurityException ex)
        {
            _logger.LogError(ex, "Security exception disabling startup");
            return Task.FromResult((false, (string?)"Access denied. You may not have permission to modify startup settings."));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Unauthorized access disabling startup");
            return Task.FromResult((false, (string?)"Access denied. Administrator privileges may be required."));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO exception disabling startup");
            return Task.FromResult((false, (string?)$"Registry error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error disabling startup");
            return Task.FromResult((false, (string?)$"Unexpected error: {ex.Message}"));
        }
    }
}
