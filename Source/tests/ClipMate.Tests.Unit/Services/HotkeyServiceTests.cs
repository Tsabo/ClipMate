using ClipMate.Core.Models;
using ClipMate.Platform;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="HotkeyService" />.
/// </summary>
public partial class HotkeyServiceTests : IDisposable
{
    private readonly IHotkeyManager _hotkeyManager;
    private readonly ILogger<HotkeyService> _logger;
    private readonly HotkeyService _service;

    public HotkeyServiceTests()
    {
        _logger = A.Fake<ILogger<HotkeyService>>();
        _hotkeyManager = A.Fake<IHotkeyManager>();
        _service = new HotkeyService(_logger, _hotkeyManager);
    }

    public void Dispose()
    {
        _service?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
#pragma warning disable CS8602 // ParamName should always be set for ArgumentNullException
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() => new HotkeyService(null!, _hotkeyManager)));
        await Assert.That(ex.ParamName).IsEqualTo("logger");
#pragma warning restore CS8602
    }

    [Test]
    public async Task Constructor_WithNullHotkeyManager_ThrowsArgumentNullException()
    {
        // Act & Assert
#pragma warning disable CS8602 // ParamName should always be set for ArgumentNullException
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() => new HotkeyService(_logger, null!)));
        await Assert.That(ex.ParamName).IsEqualTo("hotkeyManager");
#pragma warning restore CS8602
    }

    [Test]
    public async Task Constructor_WithOnlyHotkeyManager_Succeeds()
    {
        // Act
        using var service = new HotkeyService(_hotkeyManager);

        // Assert - Should create successfully
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task RegisterHotkey_WithValidParameters_ReturnsTrue()
    {
        // Arrange
        const int hotkeyId = 1;
        const ModifierKeys modifiers = ModifierKeys.Control;
        const int key = 0x43; // C key
        var action = new Action(() => { });
        A.CallTo(() => _hotkeyManager.RegisterHotkey(modifiers, key, action)).Returns(100);

        // Act
        var result = _service.RegisterHotkey(hotkeyId, modifiers, key, action);

        // Assert
        await Assert.That(result).IsTrue();
        A.CallTo(() => _hotkeyManager.RegisterHotkey(modifiers, key, action)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RegisterHotkey_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        const int hotkeyId = 1;
        const ModifierKeys modifiers = ModifierKeys.Control;
        const int key = 0x43;

        // Act & Assert
#pragma warning disable CS8602 // ParamName should always be set for ArgumentNullException
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() => _service.RegisterHotkey(hotkeyId, modifiers, key, null!)));
        await Assert.That(ex.ParamName).IsEqualTo("action");
#pragma warning restore CS8602
    }

    [Test]
    public async Task RegisterHotkey_WhenManagerThrows_ReturnsFalse()
    {
        // Arrange
        const int hotkeyId = 1;
        const ModifierKeys modifiers = ModifierKeys.Control;
        const int key = 0x43;
        var action = new Action(() => { });
        A.CallTo(() => _hotkeyManager.RegisterHotkey(modifiers, key, action))
            .Throws(new InvalidOperationException("Hotkey already in use"));

        // Act
        var result = _service.RegisterHotkey(hotkeyId, modifiers, key, action);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task RegisterHotkey_WithSameIdTwice_UnregistersFirst()
    {
        // Arrange
        const int hotkeyId = 1;
        const ModifierKeys modifiers = ModifierKeys.Control;
        const int key = 0x43;
        var action1 = new Action(() => { });
        var action2 = new Action(() => { });
        A.CallTo(() => _hotkeyManager.RegisterHotkey(modifiers, key, action1)).Returns(100);
        A.CallTo(() => _hotkeyManager.RegisterHotkey(modifiers, key, action2)).Returns(101);
        A.CallTo(() => _hotkeyManager.UnregisterHotkey(100)).Returns(true);

        // Act
        _service.RegisterHotkey(hotkeyId, modifiers, key, action1);
        _service.RegisterHotkey(hotkeyId, modifiers, key, action2);

        // Assert - Should unregister first before registering second
        A.CallTo(() => _hotkeyManager.UnregisterHotkey(100)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _hotkeyManager.RegisterHotkey(modifiers, key, action2)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task UnregisterHotkey_WithRegisteredHotkey_ReturnsTrue()
    {
        // Arrange
        const int hotkeyId = 1;
        const ModifierKeys modifiers = ModifierKeys.Control;
        const int key = 0x43;
        var action = new Action(() => { });
        A.CallTo(() => _hotkeyManager.RegisterHotkey(modifiers, key, action)).Returns(100);
        A.CallTo(() => _hotkeyManager.UnregisterHotkey(100)).Returns(true);
        _service.RegisterHotkey(hotkeyId, modifiers, key, action);

        // Act
        var result = _service.UnregisterHotkey(hotkeyId);

        // Assert
        await Assert.That(result).IsTrue();
        A.CallTo(() => _hotkeyManager.UnregisterHotkey(100)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task UnregisterHotkey_WithUnregisteredHotkey_ReturnsFalse()
    {
        // Arrange
        const int hotkeyId = 1;

        // Act
        var result = _service.UnregisterHotkey(hotkeyId);

        // Assert
        await Assert.That(result).IsFalse();
        A.CallTo(() => _hotkeyManager.UnregisterHotkey(A<int>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    public async Task UnregisterAllHotkeys_WithMultipleHotkeys_CallsManagerUnregisterAll()
    {
        // Arrange
        var action = new Action(() => { });
        A.CallTo(() => _hotkeyManager.RegisterHotkey(ModifierKeys.Control, 0x43, action)).Returns(100);
        A.CallTo(() => _hotkeyManager.RegisterHotkey(ModifierKeys.Alt, 0x56, action)).Returns(101);
        A.CallTo(() => _hotkeyManager.RegisterHotkey(ModifierKeys.Windows, 0x58, action)).Returns(102);

        _service.RegisterHotkey(1, ModifierKeys.Control, 0x43, action);
        _service.RegisterHotkey(2, ModifierKeys.Alt, 0x56, action);
        _service.RegisterHotkey(3, ModifierKeys.Windows, 0x58, action);

        // Act
        _service.UnregisterAllHotkeys();

        // Assert - Should call UnregisterAll on the manager
        A.CallTo(() => _hotkeyManager.UnregisterAll()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task UnregisterAllHotkeys_WithNoHotkeys_DoesNothing()
    {
        // Act
        _service.UnregisterAllHotkeys();

        // Assert - Should call UnregisterAll even with no hotkeys
        A.CallTo(() => _hotkeyManager.UnregisterAll()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task Dispose_CallsManagerUnregisterAll()
    {
        // Arrange
        var action = new Action(() => { });
        A.CallTo(() => _hotkeyManager.RegisterHotkey(ModifierKeys.Control, 0x43, action)).Returns(100);
        _service.RegisterHotkey(1, ModifierKeys.Control, 0x43, action);

        // Act
        _service.Dispose();

        // Assert
        A.CallTo(() => _hotkeyManager.UnregisterAll()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _hotkeyManager.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_OnlyUnregistersOnce()
    {
        // Arrange
        var action = new Action(() => { });
        A.CallTo(() => _hotkeyManager.RegisterHotkey(ModifierKeys.Control, 0x43, action)).Returns(100);
        _service.RegisterHotkey(1, ModifierKeys.Control, 0x43, action);

        // Act
        _service.Dispose();
        _service.Dispose();

        // Assert - Should only call UnregisterAll and Dispose once
        A.CallTo(() => _hotkeyManager.UnregisterAll()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _hotkeyManager.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RegisterHotkey_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var action = new Action(() => { });
        _service.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => Task.Run(() => _service.RegisterHotkey(1, ModifierKeys.Control, 0x43, action)));
    }

    [Test]
    public async Task UnregisterHotkey_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _service.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => Task.Run(() => _service.UnregisterHotkey(1)));
    }
}
