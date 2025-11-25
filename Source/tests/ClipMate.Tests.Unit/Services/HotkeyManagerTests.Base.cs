using ClipMate.Platform.Interop;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for HotkeyManager covering Win32 hotkey registration and management.
/// These tests run on STA thread since HotkeyManager requires WPF window handles.
/// </summary>
public partial class HotkeyManagerTests : TestFixtureBase
{
}
