using ClipMate.Platform.Interop;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit;

/// <summary>
/// Base class for unit tests providing common setup and utilities.
/// </summary>
public abstract class TestFixtureBase
{
    /// <summary>
    /// Creates a mock ILogger{T} that can be used in tests without strict verification.
    /// </summary>
    protected ILogger<T> CreateLogger<T>()
    {
        return A.Fake<ILogger<T>>();
    }

    /// <summary>
    /// Creates a mock IWin32ClipboardInterop for testing.
    /// </summary>
    protected IWin32ClipboardInterop CreateWin32ClipboardMock()
    {
        return A.Fake<IWin32ClipboardInterop>(o => o.Strict());
    }

    /// <summary>
    /// Creates a mock IWin32HotkeyInterop for testing.
    /// </summary>
    protected IWin32HotkeyInterop CreateWin32HotkeyMock()
    {
        return A.Fake<IWin32HotkeyInterop>(o => o.Strict());
    }

    /// <summary>
    /// Creates a mock IWin32InputInterop for testing.
    /// </summary>
    protected IWin32InputInterop CreateWin32InputMock()
    {
        return A.Fake<IWin32InputInterop>(o => o.Strict());
    }
}
