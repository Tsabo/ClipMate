using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// Another program that copies to the clipboard the way some real sources do, run by Windows PowerShell in a process
/// of its own with a hidden window as the clipboard owner. It prints what it does ("written", "rendering", ...) and
/// keeps running, so its window stays the owner, until disposed.
/// </summary>
public sealed class ClipboardTestProgram : IDisposable
{
    // C# 5, as Windows PowerShell's Add-Type compiles it
    private const string _source = """
                                   using System;
                                   using System.Runtime.InteropServices;
                                   using System.Text;
                                   using System.Threading;
                                   using System.Windows.Forms;

                                   public class ClipboardTestOwner : Form
                                   {
                                       private const int WmRenderFormat = 0x0305;
                                       private const int WmRenderAllFormats = 0x0306;
                                       private const uint CfUnicodeText = 13;
                                       private readonly string _text;
                                       private readonly int _renderDelayMs;
                                       private readonly uint _slowFor;
                                       private int _offers;

                                       private ClipboardTestOwner(string text, int renderDelayMs, uint slowFor)
                                       {
                                           _text = text;
                                           _renderDelayMs = renderDelayMs;
                                           _slowFor = slowFor;
                                       }

                                       public static void WriteInSteps(string text, int gapMs)
                                       {
                                           var owner = new ClipboardTestOwner(text, 0, 0);
                                           var window = owner.Handle;
                                           Write(window, true, CfUnicodeText, Encoding.Unicode.GetBytes(text + "\0"));
                                           Thread.Sleep(gapMs);
                                           Write(window, false, RegisterClipboardFormat("HTML Format"), Encoding.UTF8.GetBytes("<b>" + text + "</b>\0"));
                                           Thread.Sleep(gapMs);
                                           Write(window, false, RegisterClipboardFormat("Rich Text Format"), Encoding.ASCII.GetBytes("{\\rtf1 " + text + "}\0"));
                                           Console.WriteLine("written");
                                           Application.Run();
                                       }

                                       public static void OfferSlowly(string text, int renderDelayMs, uint slowFor)
                                       {
                                           var owner = new ClipboardTestOwner(text, renderDelayMs, slowFor);
                                           owner.Offer();
                                           Application.Run();
                                       }

                                       public static void CopyInBackground(string text, string title)
                                       {
                                           var owner = new ClipboardTestOwner(text, 0, 0);
                                           owner.Text = title;
                                           Write(owner.Handle, true, CfUnicodeText, Encoding.Unicode.GetBytes(text + "\0"));
                                           Console.WriteLine("written");
                                           Application.Run();
                                       }

                                       // The text is there at once for clipboard monitors that read only text; the HTML is
                                       // rendered on demand. Windows' clipboard history is kept out, as it would ask for it first.
                                       private void Offer()
                                       {
                                           _offers++;
                                           Open(Handle);
                                           EmptyClipboard();
                                           SetClipboardData(CfUnicodeText, Alloc(Encoding.Unicode.GetBytes(_text + "\0")));
                                           SetClipboardData(RegisterClipboardFormat("HTML Format"), IntPtr.Zero);
                                           SetClipboardData(RegisterClipboardFormat("CanIncludeInClipboardHistory"), Alloc(new byte[4]));
                                           SetClipboardData(RegisterClipboardFormat("CanUploadToCloudClipboard"), Alloc(new byte[4]));
                                           CloseClipboard();
                                           Console.WriteLine("offered");
                                       }

                                       private IntPtr Html()
                                       {
                                           return Alloc(Encoding.UTF8.GetBytes("<b>" + _text + "</b>\0"));
                                       }

                                       protected override void WndProc(ref Message m)
                                       {
                                           if (m.Msg == WmRenderFormat)
                                           {
                                               uint reader = 0;
                                               var opener = GetOpenClipboardWindow();
                                               if (opener != IntPtr.Zero)
                                                   GetWindowThreadProcessId(opener, out reader);

                                               if (reader != _slowFor)
                                               {
                                                   // Another clipboard reader got there first: serve it, then offer the data again
                                                   SetClipboardData(RegisterClipboardFormat("HTML Format"), Html());
                                                   Console.WriteLine("rendered for another reader " + reader);
                                                   if (_offers < 5)
                                                       BeginInvoke(new MethodInvoker(Offer));
                                                   return;
                                               }

                                               Console.WriteLine("rendering");
                                               Thread.Sleep(_renderDelayMs);
                                               SetClipboardData(RegisterClipboardFormat("HTML Format"), Html());
                                               Console.WriteLine("rendered");
                                               return;
                                           }

                                           if (m.Msg == WmRenderAllFormats)
                                               return;

                                           base.WndProc(ref m);
                                       }

                                       private static void Write(IntPtr window, bool empty, uint format, byte[] data)
                                       {
                                           Open(window);
                                           if (empty)
                                               EmptyClipboard();
                                           SetClipboardData(format, Alloc(data));
                                           CloseClipboard();
                                       }

                                       private static void Open(IntPtr window)
                                       {
                                           for (var attempt = 0; !OpenClipboard(window); attempt++)
                                           {
                                               if (attempt == 500)
                                                   throw new InvalidOperationException("Can't open the clipboard");
                                               Thread.Sleep(10);
                                           }
                                       }

                                       private static IntPtr Alloc(byte[] data)
                                       {
                                           var handle = GlobalAlloc(0x0002, new UIntPtr((uint)data.Length));
                                           Marshal.Copy(data, 0, GlobalLock(handle), data.Length);
                                           GlobalUnlock(handle);
                                           return handle;
                                       }

                                       [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr window);
                                       [DllImport("user32.dll")] private static extern bool CloseClipboard();
                                       [DllImport("user32.dll")] private static extern bool EmptyClipboard();
                                       [DllImport("user32.dll")] private static extern IntPtr GetOpenClipboardWindow();
                                       [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
                                       [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr data);
                                       [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
                                       [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
                                       [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr handle);
                                       [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr handle);
                                   }
                                   """;

    private readonly ConcurrentQueue<string> _errors = new();
    private readonly ConcurrentQueue<string> _output = new();
    private readonly Process _process;

    private ClipboardTestProgram(string call)
    {
        var script = $"""
                      $ProgressPreference = 'SilentlyContinue'
                      Add-Type -AssemblyName System.Windows.Forms
                      Add-Type -ReferencedAssemblies System.Windows.Forms -TypeDefinition @'
                      {_source}
                      '@
                      {call}
                      """;

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        _process = Process.Start(new ProcessStartInfo("powershell.exe", ["-NoProfile", "-NonInteractive", "-EncodedCommand", encoded])
                   {
                       UseShellExecute = false,
                       CreateNoWindow = true,
                       RedirectStandardOutput = true,
                       RedirectStandardError = true,
                   })
                   ?? throw new InvalidOperationException("Failed to start PowerShell");

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                _output.Enqueue(e.Data.Trim());
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _errors.Enqueue(e.Data);
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    /// <summary>
    /// Copies <paramref name="text" /> as Unicode text, then adds "HTML Format" and then "Rich Text Format", each in its
    /// own open and close of the clipboard <paramref name="gapMs" /> apart, as a source that writes its formats one at a
    /// time does. Prints "written" when done.
    /// </summary>
    public static ClipboardTestProgram WriteInSteps(string text, int gapMs) =>
        new($"[ClipboardTestOwner]::WriteInSteps('{Quote(text)}', {gapMs})");

    /// <summary>
    /// Offers <paramref name="text" /> as Unicode text plus delayed-rendered HTML ("offered"), and supplies the HTML only
    /// <paramref name="renderDelayMs" /> after <paramref name="reader" /> asks for it ("rendering", then "rendered").
    /// Another program that asks first (a clipboard monitor the user runs) gets it at once, and the data is offered again.
    /// </summary>
    public static ClipboardTestProgram OfferSlowly(string text, int renderDelayMs, Process reader) =>
        new($"[ClipboardTestOwner]::OfferSlowly('{Quote(text)}', {renderDelayMs}, {reader.Id})");

    /// <summary>
    /// Copies <paramref name="text" /> from a hidden window titled <paramref name="title" />, never in the foreground.
    /// Prints "written" when done.
    /// </summary>
    public static ClipboardTestProgram CopyInBackground(string text, string title) =>
        new($"[ClipboardTestOwner]::CopyInBackground('{Quote(text)}', '{Quote(title)}')");

    /// <summary>
    /// The name of the program's process, as ClipMate records it.
    /// </summary>
    public string ProcessName => _process.ProcessName;

    /// <summary>
    /// True once the program has printed <paramref name="line" />. Throws when it has exited without printing it.
    /// </summary>
    public bool HasPrinted(string line)
    {
        if (_output.Contains(line))
            return true;

        return _process.HasExited
            ? throw new InvalidOperationException(
                $"The clipboard test program exited (code {_process.ExitCode}) before printing \"{line}\": {string.Join(Environment.NewLine, _errors)}")
            : false;
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(true);
            _process.WaitForExit();
        }

        _process.Dispose();
    }

    private static string Quote(string text) => text.Replace("'", "''", StringComparison.Ordinal);
}
