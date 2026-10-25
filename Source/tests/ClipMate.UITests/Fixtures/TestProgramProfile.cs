using System.IO;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// The Application Profile ClipMate gives the test program, written before ClipMate starts. As in ClipMate 7, a new
/// program gets text and HTML (or the image when there is no text) and every other Format starts off, so a test that
/// copies an image with text, a file list or a private Format switches it on, as a user would.
/// <para>
/// Copy with <see cref="ForegroundTestWindow.Copy" />, so the test program owns the clipboard, and keep the window open
/// until the test ends: when its thread ends, OLE puts the copy on the clipboard again, which ClipMate sees as a new change.
/// </para>
/// </summary>
public static class TestProgramProfile
{
    public static void Write(AppFixture appFixture, params string[] formatsOn)
    {
        var program = System.Diagnostics.Process.GetCurrentProcess().ProcessName.ToUpperInvariant();
        var settings = string.Concat(formatsOn.Select(p => $"\"{p}\" = true\n"));
        Directory.CreateDirectory(appFixture.DataDirectory);
        File.WriteAllText(Path.Combine(appFixture.DataDirectory, "application-profiles.toml"),
            $"[profiles.\"{program}\"]\nenabled = true\nCF_TEXT = true\n{settings}");
    }
}
