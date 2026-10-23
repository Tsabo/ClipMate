using Windows.Win32.Foundation;
using ClipMate.Platform.Helpers;
using ClipMate.Platform.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteClipboardBridgeTests
{
    private const uint _ownProcessId = 100;

    [Test]
    [Category("IsPaste")]
    public async Task IsPaste_ReadByTheForegroundApplication_IsAPaste() =>
        await Assert.That(Reader(200, 200).IsPaste(_ownProcessId)).IsTrue();

    [Test]
    [Category("IsPaste")]
    public async Task IsPaste_ReadByABackgroundApplication_IsNotAPaste() =>
        // e.g. clipboard history or another clipboard manager reading while the user works in another application
        await Assert.That(Reader(300, 200).IsPaste(_ownProcessId)).IsFalse();

    [Test]
    [Category("IsPaste")]
    public async Task IsPaste_ReadByClipMateInTheForeground_IsNotAPaste() =>
        await Assert.That(Reader(_ownProcessId, _ownProcessId).IsPaste(_ownProcessId)).IsFalse();

    [Test]
    [Category("IsPaste")]
    public async Task IsPaste_ReadWithoutAWindow_IsNotAPaste() =>
        // OpenClipboard(NULL): the reader cannot be told apart from a background one
        await Assert.That(Reader(0, 0).IsPaste(_ownProcessId)).IsFalse();

    [Test]
    [Category("IsPaste")]
    public async Task Current_PasteByTheForegroundEditor_IsAPaste()
    {
        var desktop = new FakeDesktop();
        var editor = desktop.Open("NOTEPAD", "Notepad", "notes.txt");
        desktop.Activate(editor);

        var reader = ClipboardReader.Current(desktop, new HWND(editor.Handle));

        await Assert.That(reader.ProcessName).IsEqualTo("NOTEPAD");
        await Assert.That(reader.IsPaste(OwnProcess.Id)).IsTrue();
    }

    [Test]
    [Category("IsPaste")]
    public async Task Current_ReadByABackgroundWindowWhileTheEditorIsInFront_IsNotAPaste()
    {
        var desktop = new FakeDesktop();
        var history = desktop.Open("CLIPHISTORY", "HistoryWindow", "Clipboard history");
        var editor = desktop.Open("NOTEPAD", "Notepad", "notes.txt");
        desktop.Activate(editor);

        var reader = ClipboardReader.Current(desktop, new HWND(history.Handle));

        await Assert.That(reader.ForegroundProcessName).IsEqualTo("NOTEPAD");
        await Assert.That(reader.IsPaste(OwnProcess.Id)).IsFalse();
    }

    [Test]
    [Category("IsPaste")]
    public async Task Current_ReadByClipMateInFront_IsNotAPaste()
    {
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        desktop.Activate(clipMate);

        var reader = ClipboardReader.Current(desktop, new HWND(clipMate.Handle));

        await Assert.That(reader.IsPaste(OwnProcess.Id)).IsFalse();
    }

    [Test]
    [Category("IsPaste")]
    public async Task Current_ReadWithoutAWindow_NamesNoWindow()
    {
        var desktop = new FakeDesktop();
        desktop.Activate(desktop.Open("NOTEPAD", "Notepad", "notes.txt"));

        var reader = ClipboardReader.Current(desktop, HWND.Null);

        await Assert.That(reader.ProcessName).IsEqualTo("(no window)");
        await Assert.That(reader.IsPaste(OwnProcess.Id)).IsFalse();
    }

    private static ClipboardReader Reader(uint processId, uint foregroundProcessId) =>
        new(processId, "reader", foregroundProcessId, "foreground");
}
