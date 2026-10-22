using ClipMate.Platform.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteClipboardBridgeTests
{
    private const uint _ownProcessId = 100;

    [Test]
    [Category("IsPaste")]
    public async Task IsPaste_ReadByTheForegroundApplication_IsAPaste() =>
        await Assert.That(PowerPasteClipboardBridge.IsPaste(200, 200, _ownProcessId)).IsTrue();

    [Test]
    [Category("IsPaste")]
    public async Task IsPaste_ReadByABackgroundApplication_IsNotAPaste() =>
        // e.g. clipboard history or another clipboard manager reading while the user works in another application
        await Assert.That(PowerPasteClipboardBridge.IsPaste(300, 200, _ownProcessId)).IsFalse();

    [Test]
    [Category("IsPaste")]
    public async Task IsPaste_ReadByClipMateInTheForeground_IsNotAPaste() =>
        await Assert.That(PowerPasteClipboardBridge.IsPaste(_ownProcessId, _ownProcessId, _ownProcessId)).IsFalse();

    [Test]
    [Category("IsPaste")]
    public async Task IsPaste_ReadWithoutAWindow_IsNotAPaste() =>
        // OpenClipboard(NULL): the reader cannot be told apart from a background one
        await Assert.That(PowerPasteClipboardBridge.IsPaste(0, 0, _ownProcessId)).IsFalse();
}
