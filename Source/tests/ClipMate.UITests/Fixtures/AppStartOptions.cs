namespace ClipMate.UITests.Fixtures;

/// <summary>
/// How <see cref="AppFixture" /> starts ClipMate. The defaults are what the UI tests need; a copy that must leave the
/// clipboard alone sets <see cref="SkipStartupCapture" /> and clears <see cref="WaitUntilCapturing" />.
/// </summary>
public sealed record AppStartOptions
{
    /// <summary>
    /// The executable to run; null for the ClipMate.App.exe built beside the tests.
    /// </summary>
    public string? ExePath { get; init; }

    /// <summary>
    /// Settings to change first, as section.key=value with the value written as TOML: preferences.default_editor_view="Markdown".
    /// A section may be dotted: preferences.classic_window_taskbar.left=100.
    /// </summary>
    public IReadOnlyList<string> Settings { get; init; } = [];

    /// <summary>
    /// Starts every run with --no-startup-capture, so the copy never stores what is on the user's clipboard.
    /// </summary>
    public bool SkipStartupCapture { get; init; }

    /// <summary>
    /// Waits for a marker put on the clipboard to show up in the clip list. Turned off, nothing is written to the clipboard.
    /// </summary>
    public bool WaitUntilCapturing { get; init; } = true;

    /// <summary>
    /// Runs after ClipMate's first run has written its defaults and stopped, before the run under test: the moment to
    /// replace a file in the data folder.
    /// </summary>
    public Func<AppFixture, Task>? AfterFirstRun { get; init; }
}
