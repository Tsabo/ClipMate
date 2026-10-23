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

    private static ClipboardReader Reader(uint processId, uint foregroundProcessId) =>
        new(processId, "reader", foregroundProcessId, "foreground");
}
