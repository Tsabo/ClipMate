using ClipMate.Platform;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for HotkeyService covering registration, unregistration, and disposal.
/// </summary>
public partial class HotkeyServiceTests : TestFixtureBase
{
    private Mock<HotkeyManager> CreateMockHotkeyManager()
    {
        // HotkeyManager doesn't have an interface, so we mock it directly
        // Note: This requires HotkeyManager methods to be virtual for mocking
        var mock = new Mock<HotkeyManager>(MockBehavior.Loose);
        return mock;
    }
}
