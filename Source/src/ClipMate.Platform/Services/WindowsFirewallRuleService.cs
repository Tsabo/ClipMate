using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Adds the inbound Windows Firewall rule for ClipMate with <c>netsh advfirewall</c>.
/// ClipMate runs without administrator rights, so the rule is added through an elevated netsh
/// and Windows shows its standard elevation (UAC) prompt. Checking for the rule needs no elevation.
/// </summary>
public sealed class WindowsFirewallRuleService : IFirewallRuleService
{
    /// <summary>
    /// Name of the firewall rule ClipMate manages.
    /// </summary>
    internal const string RuleName = "ClipMate Peer Networking";

    private const int _errorCancelled = 1223; // ERROR_CANCELLED: the user declined the UAC prompt

    private readonly ILogger<WindowsFirewallRuleService> _logger;
    private readonly string _programPath;
    private readonly INetshRunner _runner;

    /// <summary>
    /// Creates the service for the running ClipMate executable.
    /// </summary>
    public WindowsFirewallRuleService(ILogger<WindowsFirewallRuleService> logger)
        : this(new NetshRunner(), Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the ClipMate executable path."), logger)
    {
    }

    internal WindowsFirewallRuleService(INetshRunner runner, string programPath, ILogger<WindowsFirewallRuleService> logger)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        ArgumentException.ThrowIfNullOrWhiteSpace(programPath);
        _programPath = programPath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> EnsureInboundRuleAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _runner.RunAsync($"advfirewall firewall show rule name=\"{RuleName}\" verbose", false, cancellationToken);
        if (existing.ExitCode == 0 && existing.Output.Contains(_programPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Firewall rule {RuleName} already exists", RuleName);
            return true;
        }

        try
        {
            var added = await _runner.RunAsync(
                $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow program=\"{_programPath}\" enable=yes profile=private,domain",
                true,
                cancellationToken);

            if (added.ExitCode == 0)
            {
                _logger.LogInformation("Added firewall rule {RuleName} for {ProgramPath}", RuleName, _programPath);
                return true;
            }

            _logger.LogWarning("netsh failed to add firewall rule {RuleName} (exit code {ExitCode})", RuleName, added.ExitCode);
            return false;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == _errorCancelled)
        {
            _logger.LogWarning("Firewall rule {RuleName} was not added: the elevation prompt was declined", RuleName);
            return false;
        }
    }
}

/// <summary>
/// Result of a netsh invocation. <see cref="Output" /> is empty for elevated runs, whose output cannot be captured.
/// </summary>
internal sealed record NetshResult(int ExitCode, string Output);

/// <summary>
/// Runs netsh.exe; seam for testing <see cref="WindowsFirewallRuleService" />.
/// </summary>
internal interface INetshRunner
{
    /// <summary>
    /// Runs netsh with <paramref name="arguments" />, elevated through the UAC prompt if <paramref name="elevated" />.
    /// Throws <see cref="Win32Exception" /> (1223) if the user declines elevation.
    /// </summary>
    Task<NetshResult> RunAsync(string arguments, bool elevated, CancellationToken cancellationToken);
}

/// <summary>
/// Runs the real netsh.exe.
/// </summary>
internal sealed class NetshRunner : INetshRunner
{
    /// <inheritdoc />
    public async Task<NetshResult> RunAsync(string arguments, bool elevated, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe"),
            Arguments = arguments,
        };

        if (elevated)
        {
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        }
        else
        {
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start netsh.");
        var output = elevated ? string.Empty : await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new NetshResult(process.ExitCode, output);
    }
}
