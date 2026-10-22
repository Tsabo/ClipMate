using System.Diagnostics;
using System.Text;
using System.Windows.Automation;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// A window to paste into: a WinForms form with a single-line text box (UI Automation reports a multiline one as a
/// document without a value), run by Windows PowerShell in a process of its own.
/// It has to be another program, since ClipMate never targets its own windows and the BAD list rejects the test
/// process ("CLIPMATE:" matches "ClipMate.UITests").
/// </summary>
public sealed class TargetWindow : IDisposable
{
    private readonly Process _process;

    private TargetWindow(Process process, string title, nint handle)
    {
        _process = process;
        Title = title;
        Handle = handle;
    }

    public string Title { get; }

    public nint Handle { get; }

    /// <summary>
    /// Starts the window and waits until it is shown.
    /// </summary>
    public static async Task<TargetWindow> OpenAsync(string title)
    {
        var script = $$"""
                       Add-Type -AssemblyName System.Windows.Forms
                       $form = New-Object System.Windows.Forms.Form
                       $form.Text = '{{title.Replace("'", "''", StringComparison.Ordinal)}}'
                       $form.Width = 500
                       $form.Height = 300
                       $box = New-Object System.Windows.Forms.TextBox
                       $box.Dock = 'Top'
                       $form.Controls.Add($box)
                       [System.Windows.Forms.Application]::Run($form)
                       """;

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var process = Process.Start(new ProcessStartInfo("powershell.exe", ["-NoProfile", "-NonInteractive", "-EncodedCommand", encoded])
                      {
                          UseShellExecute = false,
                          CreateNoWindow = true,
                      })
                      ?? throw new InvalidOperationException("Failed to start PowerShell");

        try
        {
            var handle = await Poll.UntilAsync(() =>
            {
                process.Refresh();
                return process.MainWindowHandle != 0 && process.MainWindowTitle == title
                    ? (object)process.MainWindowHandle
                    : null;
            }, $"the \"{title}\" window", TimeSpan.FromSeconds(30));

            return new TargetWindow(process, title, (nint)handle);
        }
        catch
        {
            process.Kill(true);
            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The text box's content, read through UI Automation.
    /// </summary>
    public string ReadText()
    {
        var edit = AutomationElement.FromHandle(Handle)
            .FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));

        return edit == null
            ? string.Empty
            : ((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
    }

    /// <summary>
    /// Closes the window by ending its process.
    /// </summary>
    public void Close()
    {
        if (_process.HasExited)
            return;

        _process.Kill(true);
        _process.WaitForExit();
    }

    public void Dispose()
    {
        Close();
        _process.Dispose();
    }
}
