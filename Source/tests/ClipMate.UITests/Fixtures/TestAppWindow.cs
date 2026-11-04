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
public sealed class TestAppWindow : IDisposable
{
    private readonly Process _process;

    private TestAppWindow(Process process, string title, nint handle)
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
    public static async Task<TestAppWindow> OpenAsync(string title, TestAppKind kind = TestAppKind.WinForms)
    {
        var quotedTitle = title.Replace("'", "''", StringComparison.Ordinal);
        var script = kind switch
        {
            TestAppKind.Wpf => $$"""
                                 Add-Type -AssemblyName PresentationFramework
                                 $window = New-Object System.Windows.Window
                                 $window.Title = '{{quotedTitle}}'
                                 $window.Width = 500
                                 $window.Height = 300
                                 $box = New-Object System.Windows.Controls.TextBox
                                 $box.VerticalAlignment = 'Top'
                                 $window.Content = $box
                                 $window.Add_Loaded({ $box.Focus() | Out-Null })
                                 [void]$window.ShowDialog()
                                 """,
            TestAppKind.NoWindowReader => $$"""
                                            Add-Type -AssemblyName System.Windows.Forms
                                            Add-Type -TypeDefinition @'
                                            using System;
                                            using System.Runtime.InteropServices;
                                            public static class NoWindowClipboard
                                            {
                                                public static string ReadText()
                                                {
                                                    if (!OpenClipboard(IntPtr.Zero))
                                                        return string.Empty;
                                                    try
                                                    {
                                                        var data = GetClipboardData(13);
                                                        if (data == IntPtr.Zero)
                                                            return string.Empty;
                                                        var text = Marshal.PtrToStringUni(GlobalLock(data));
                                                        GlobalUnlock(data);
                                                        return text;
                                                    }
                                                    finally
                                                    {
                                                        CloseClipboard();
                                                    }
                                                }
                                                [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr window);
                                                [DllImport("user32.dll")] private static extern bool CloseClipboard();
                                                [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
                                                [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr handle);
                                                [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr handle);
                                            }
                                            '@
                                            $form = New-Object System.Windows.Forms.Form
                                            $form.Text = '{{quotedTitle}}'
                                            $form.Width = 500
                                            $form.Height = 300
                                            $box = New-Object System.Windows.Forms.TextBox
                                            $box.Dock = 'Top'
                                            $box.Add_KeyDown({
                                                if ($_.Control -and $_.KeyCode -eq 'V') {
                                                    $_.SuppressKeyPress = $true
                                                    $box.AppendText([NoWindowClipboard]::ReadText())
                                                }
                                            })
                                            $form.Controls.Add($box)
                                            [System.Windows.Forms.Application]::Run($form)
                                            """,
            _ => $$"""
                   Add-Type -AssemblyName System.Windows.Forms
                   $form = New-Object System.Windows.Forms.Form
                   $form.Text = '{{quotedTitle}}'
                   $form.Width = 500
                   $form.Height = 300
                   $box = New-Object System.Windows.Forms.TextBox
                   $box.Dock = 'Top'
                   $form.Controls.Add($box)
                   [System.Windows.Forms.Application]::Run($form)
                   """,
        };

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

            return new TestAppWindow(process, title, (nint)handle);
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
