using ClipMate.Platform;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for HotkeyService covering registration, unregistration, and disposal.
/// </summary>
public partial class HotkeyServiceTests : TestFixtureBase
{
    private IHotkeyManager CreateMockHotkeyManager()
    {
        var mock = A.Fake<IHotkeyManager>();
        // Setup UnregisterHotkey to return true by default
        A.CallTo(() => mock.UnregisterHotkey(A<int>.Ignored)).Returns(true);
        return mock;
    }
}
