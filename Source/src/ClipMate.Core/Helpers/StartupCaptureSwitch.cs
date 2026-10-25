namespace ClipMate.Core.Helpers;

/// <summary>
/// The <c>--no-startup-capture</c> command-line switch (or the <see cref="SkipVariable" /> environment variable): skips
/// "Capture existing clipboard at startup" whatever the settings say, so a test copy started beside the user's own
/// ClipMate never stores what the user last copied.
/// </summary>
public static class StartupCaptureSwitch
{
    /// <summary>
    /// The environment variable that, set to <c>1</c>, skips the startup capture.
    /// </summary>
    public const string SkipVariable = "CLIPMATE_NO_STARTUP_CAPTURE";

    /// <summary>
    /// Gets whether the startup capture is skipped.
    /// </summary>
    public static bool IsSkipped => Environment.GetEnvironmentVariable(SkipVariable) == "1";

    /// <summary>
    /// Applies a <c>--no-startup-capture</c> switch from <paramref name="args" /> to this process.
    /// </summary>
    public static void ApplyCommandLine(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Any(p => p.Equals("--no-startup-capture", StringComparison.OrdinalIgnoreCase)))
            Environment.SetEnvironmentVariable(SkipVariable, "1");
    }
}
