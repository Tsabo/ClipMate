using ClipMate.Platform;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for HotkeyService covering registration, unregistration, and disposal.
/// </summary>
public partial class HotkeyServiceTests : TestFixtureBase
{
    private Mock<IHotkeyManager> CreateMockHotkeyManager()
    {
        var mock = new Mock<IHotkeyManager>();
        // Setup UnregisterHotkey to return true by default
        mock.Setup(m => m.UnregisterHotkey(It.IsAny<int>())).Returns(true);
        return mock;
    }
}
