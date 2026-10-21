using ClipMate.Core.Helpers;

namespace ClipMate.Tests.Unit.Helpers;

/// <summary>
/// <see cref="ClipMatePaths" /> reads a process-wide environment variable, so these tests never run alongside others.
/// </summary>
[NotInParallel]
public class ClipMatePathsTests
{
    private string? _saved;

    [Before(Test)]
    public void Setup()
    {
        _saved = Environment.GetEnvironmentVariable(ClipMatePaths.DataDirectoryVariable);
        Environment.SetEnvironmentVariable(ClipMatePaths.DataDirectoryVariable, null);
    }

    [After(Test)]
    public void Cleanup() => Environment.SetEnvironmentVariable(ClipMatePaths.DataDirectoryVariable, _saved);

    [Test]
    public async Task DataDirectory_NotOverridden_IsLocalAppDataClipMate()
    {
        // Assert
        await Assert.That(ClipMatePaths.IsDataDirectoryOverridden).IsFalse();
        await Assert.That(ClipMatePaths.DataDirectory).IsEqualTo(ClipMatePaths.DefaultDataDirectory);
        await Assert.That(ClipMatePaths.DataDirectory).EndsWith(Path.DirectorySeparatorChar + "ClipMate");
    }

    [Test]
    [Arguments("--data-dir", @"C:\Temp\clipmate-a")]
    [Arguments("--DATA-DIR", @"C:\Temp\clipmate-a")]
    public async Task ApplyCommandLine_SwitchAndValue_MovesTheDataDirectory(string flag, string folder)
    {
        // Act
        var applied = ClipMatePaths.ApplyCommandLine(["--force-backup-prompt", flag, folder]);

        // Assert
        await Assert.That(applied).IsEqualTo(@"C:\Temp\clipmate-a");
        await Assert.That(ClipMatePaths.IsDataDirectoryOverridden).IsTrue();
        await Assert.That(ClipMatePaths.LogDirectory).IsEqualTo(@"C:\Temp\clipmate-a\Logs");
        await Assert.That(ClipMatePaths.DefaultTemplatesDirectory).IsEqualTo(@"C:\Temp\clipmate-a\Templates");
    }

    [Test]
    public async Task ApplyCommandLine_EqualsForm_MovesTheDataDirectory()
    {
        // Act
        ClipMatePaths.ApplyCommandLine([@"--data-dir=""C:\Temp\clipmate b"""]);

        // Assert
        await Assert.That(ClipMatePaths.DataDirectory).IsEqualTo(@"C:\Temp\clipmate b");
    }

    [Test]
    public async Task ApplyCommandLine_NoSwitchOrNoValue_LeavesTheDefault()
    {
        // Act
        var none = ClipMatePaths.ApplyCommandLine(["--force-backup-prompt"]);
        var dangling = ClipMatePaths.ApplyCommandLine(["--data-dir"]);

        // Assert
        await Assert.That(none).IsNull();
        await Assert.That(dangling).IsNull();
        await Assert.That(ClipMatePaths.IsDataDirectoryOverridden).IsFalse();
    }
}
