namespace ClipMate.Core.Helpers;

/// <summary>
/// Where ClipMate keeps its settings, databases, logs and Device identity. Normally
/// <c>%LOCALAPPDATA%\ClipMate</c>; the <c>--data-dir</c> command-line switch (or the <see cref="DataDirectoryVariable" />
/// environment variable) moves all of it to another folder, so a second copy can run isolated from the user's own data,
/// e.g. for a smoke test or two Devices on one computer.
/// </summary>
public static class ClipMatePaths
{
    /// <summary>
    /// The environment variable that overrides <see cref="DataDirectory" />.
    /// </summary>
    public const string DataDirectoryVariable = "CLIPMATE_DATA_DIR";

    /// <summary>
    /// Gets the folder ClipMate keeps its data in.
    /// </summary>
    public static string DataDirectory => OverriddenDataDirectory ?? DefaultDataDirectory;

    /// <summary>
    /// Gets whether <see cref="DataDirectory" /> was moved away from the default.
    /// </summary>
    public static bool IsDataDirectoryOverridden => OverriddenDataDirectory != null;

    /// <summary>
    /// Gets the default data folder, <c>%LOCALAPPDATA%\ClipMate</c>.
    /// </summary>
    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipMate");

    /// <summary>
    /// Gets the log folder.
    /// </summary>
    public static string LogDirectory => Path.Combine(DataDirectory, "Logs");

    /// <summary>
    /// Gets the default templates folder: roaming (<c>%APPDATA%\ClipMate\Templates</c>) normally, or inside the
    /// overridden data folder so an isolated copy shares nothing with the user's own.
    /// </summary>
    public static string DefaultTemplatesDirectory => IsDataDirectoryOverridden
        ? Path.Combine(DataDirectory, "Templates")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipMate", "Templates");

    /// <summary>
    /// Applies a <c>--data-dir &lt;folder&gt;</c> or <c>--data-dir=&lt;folder&gt;</c> switch from
    /// <paramref name="args" /> to this process. Call before anything reads <see cref="DataDirectory" />.
    /// </summary>
    /// <returns>The overriding folder, or <c>null</c> if the switch is absent.</returns>
    public static string? ApplyCommandLine(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string? value = null;
            if (arg.Equals("--data-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
                value = args[i + 1];
            else if (arg.StartsWith("--data-dir=", StringComparison.OrdinalIgnoreCase))
                value = arg["--data-dir=".Length..];

            if (string.IsNullOrWhiteSpace(value))
                continue;

            Environment.SetEnvironmentVariable(DataDirectoryVariable, value);
            return DataDirectory;
        }

        return null;
    }

    private static string? OverriddenDataDirectory =>
        Environment.GetEnvironmentVariable(DataDirectoryVariable) is { Length: > 0 } value
            ? Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')))
            : null;
}
