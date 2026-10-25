using ClipMate.Core.Helpers;

namespace ClipMate.Tests.Unit.Helpers;

/// <summary>
/// <see cref="StartupCaptureSwitch" /> reads a process-wide environment variable, so these tests never run alongside others.
/// </summary>
[NotInParallel]
public class StartupCaptureSwitchTests
{
    private string? _saved;

    [Before(Test)]
    public void Setup()
    {
        _saved = Environment.GetEnvironmentVariable(StartupCaptureSwitch.SkipVariable);
        Environment.SetEnvironmentVariable(StartupCaptureSwitch.SkipVariable, null);
    }

    [After(Test)]
    public void Cleanup() => Environment.SetEnvironmentVariable(StartupCaptureSwitch.SkipVariable, _saved);

    [Test]
    [Arguments("--no-startup-capture")]
    [Arguments("--NO-STARTUP-CAPTURE")]
    public async Task ApplyCommandLine_Switch_SkipsTheStartupCapture(string flag)
    {
        // Act
        StartupCaptureSwitch.ApplyCommandLine(["--data-dir", @"C:\Temp\clipmate-a", flag]);

        // Assert
        await Assert.That(StartupCaptureSwitch.IsSkipped).IsTrue();
    }

    [Test]
    public async Task ApplyCommandLine_NoSwitch_KeepsTheStartupCapture()
    {
        // Act
        StartupCaptureSwitch.ApplyCommandLine(["--data-dir", @"C:\Temp\clipmate-a"]);

        // Assert
        await Assert.That(StartupCaptureSwitch.IsSkipped).IsFalse();
    }
}
