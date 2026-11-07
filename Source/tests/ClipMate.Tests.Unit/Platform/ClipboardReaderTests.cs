using Windows.Win32.Foundation;
using ClipMate.Platform.Helpers;
using ClipMate.Platform.Services;
using ClipMate.Tests.Unit.Services;

namespace ClipMate.Tests.Unit.Platform;

[Category("Unit")]
public class ClipboardReaderTests
{
    private const uint _ownProcessId = 100;

    [Test]
    [Category("IsForegroundApplication")]
    public async Task IsForegroundApplication_ReadByTheForegroundApplication_IsAPaste() =>
        await Assert.That(Reader(200, 200).IsForegroundApplication(_ownProcessId)).IsTrue();

    [Test]
    [Category("IsForegroundApplication")]
    public async Task IsForegroundApplication_ReadByABackgroundApplication_IsNotAPaste() =>
        // e.g. clipboard history or another clipboard manager reading while the user works in another application
        await Assert.That(Reader(300, 200).IsForegroundApplication(_ownProcessId)).IsFalse();

    [Test]
    [Category("IsForegroundApplication")]
    public async Task IsForegroundApplication_ReadByClipMateInTheForeground_IsNotAPaste() =>
        await Assert.That(Reader(_ownProcessId, _ownProcessId).IsForegroundApplication(_ownProcessId)).IsFalse();

    [Test]
    [Category("IsForegroundApplication")]
    public async Task IsForegroundApplication_ReadWithoutAWindow_IsNotAPaste() =>
        // OpenClipboard(NULL): the reader cannot be told apart from a background one
        await Assert.That(Reader(0, 0).IsForegroundApplication(_ownProcessId)).IsFalse();

    [Test]
    [Category("IsForegroundApplication")]
    public async Task Current_PasteByTheForegroundEditor_IsAPaste()
    {
        var desktop = new FakeDesktop();
        var editor = desktop.Open("NOTEPAD", "Notepad", "notes.txt");
        desktop.Activate(editor);

        var reader = ClipboardReader.Current(desktop, new HWND(editor.Handle));

        await Assert.That(reader.ProcessName).IsEqualTo("NOTEPAD");
        await Assert.That(reader.IsForegroundApplication(OwnProcess.Id)).IsTrue();
    }

    [Test]
    [Category("IsForegroundApplication")]
    public async Task Current_ReadByABackgroundWindowWhileTheEditorIsInFront_IsNotAPaste()
    {
        var desktop = new FakeDesktop();
        var history = desktop.Open("CLIPHISTORY", "HistoryWindow", "Clipboard history");
        var editor = desktop.Open("NOTEPAD", "Notepad", "notes.txt");
        desktop.Activate(editor);

        var reader = ClipboardReader.Current(desktop, new HWND(history.Handle));

        await Assert.That(reader.ForegroundProcessName).IsEqualTo("NOTEPAD");
        await Assert.That(reader.IsForegroundApplication(OwnProcess.Id)).IsFalse();
    }

    [Test]
    [Category("IsForegroundApplication")]
    public async Task Current_ReadByClipMateInFront_IsNotAPaste()
    {
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        desktop.Activate(clipMate);

        var reader = ClipboardReader.Current(desktop, new HWND(clipMate.Handle));

        await Assert.That(reader.IsForegroundApplication(OwnProcess.Id)).IsFalse();
    }

    [Test]
    [Category("IsForegroundApplication")]
    public async Task Current_ReadWithoutAWindow_NamesNoWindow()
    {
        var desktop = new FakeDesktop();
        desktop.Activate(desktop.Open("NOTEPAD", "Notepad", "notes.txt"));

        var reader = ClipboardReader.Current(desktop, HWND.Null);

        await Assert.That(reader.ProcessName).IsEqualTo("(no window)");
        await Assert.That(reader.IsForegroundApplication(OwnProcess.Id)).IsFalse();
    }

    [Test]
    [Category("IsWithoutWindow")]
    public async Task IsWithoutWindow_ReadWithoutAWindow_IsTrue() =>
        // Edge and Windows Terminal, observed in the log: "read by (no window) while msedge is in the foreground"
        await Assert.That(new ClipboardReader(0, "(no window)", 200, "msedge").IsWithoutWindow).IsTrue();

    [Test]
    [Category("IsWithoutWindow")]
    public async Task IsWithoutWindow_ANamedBackgroundProgram_IsFalse() =>
        await Assert.That(new ClipboardReader(300, "msrdc", 200, "notepad").IsWithoutWindow).IsFalse();

    [Test]
    [Category("IsStoreAppInForeground")]
    public async Task IsStoreAppInForeground_WithTheFrameHostInFront_IsTrue() =>
        // Calculator, observed in the log: "read by svchost while ApplicationFrameHost is in the foreground"
        await Assert.That(new ClipboardReader(300, "svchost", 200, "ApplicationFrameHost").IsStoreAppInForeground).IsTrue();

    [Test]
    [Category("IsStoreAppInForeground")]
    public async Task IsStoreAppInForeground_WithADesktopProgramInFront_IsFalse() =>
        await Assert.That(new ClipboardReader(300, "svchost", 200, "notepad").IsStoreAppInForeground).IsFalse();

    private static ClipboardReader Reader(uint processId, uint foregroundProcessId) =>
        new(processId, "reader", foregroundProcessId, "foreground");
}
