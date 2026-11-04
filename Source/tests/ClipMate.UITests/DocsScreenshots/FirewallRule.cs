using System.Diagnostics;
using System.IO;

namespace ClipMate.UITests.DocsScreenshots;

/// <summary>
/// Checks for the firewall rule ClipMate wants when networking is on, the way ClipMate does, without elevation. With no
/// rule for its exact path, ClipMate starts an elevated netsh, so a copy must not be started with networking on until
/// this has passed: the script never causes a UAC prompt.
/// </summary>
internal static class FirewallRule
{
    private const string _ruleName = "ClipMate Peer Networking";

    /// <summary>
    /// Throws, naming what to do, unless the rule lists <paramref name="exePath" />.
    /// </summary>
    public static async Task RequireForAsync(string exePath)
    {
        using var netsh = Process.Start(new ProcessStartInfo
                          {
                              FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe"),
                              Arguments = $"advfirewall firewall show rule name=\"{_ruleName}\" verbose",
                              UseShellExecute = false,
                              CreateNoWindow = true,
                              RedirectStandardOutput = true,
                          })
                          ?? throw new InvalidOperationException("Could not start netsh");

        var output = await netsh.StandardOutput.ReadToEndAsync();
        await netsh.WaitForExitAsync();
        if (netsh.ExitCode != 0 || !output.Contains(exePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The firewall rule \"{_ruleName}\" does not list {exePath}, so the Pairing shots were not taken and no " +
                                                "networking copy was started. Run that exe once with networking on and approve the Windows prompt, or pass --exe with a path that has the rule.");
        }
    }
}
