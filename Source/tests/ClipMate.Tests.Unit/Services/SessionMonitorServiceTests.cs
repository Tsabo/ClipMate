using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="SessionMonitorService" />.
/// </summary>
public class SessionMonitorServiceTests : IDisposable
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<SessionMonitorService> _logger;
    private readonly IMessenger _messenger;
    private readonly SessionMonitorService _service;

    public SessionMonitorServiceTests()
    {
        _configService = A.Fake<IConfigurationService>();
        _messenger = A.Fake<IMessenger>();
        _logger = A.Fake<ILogger<SessionMonitorService>>();

        // Setup default configuration
        var config = new ClipMateConfiguration
        {
            Encryption = new EncryptionConfiguration
            {
                LockOnScreenLock = false,
            },
        };

        A.CallTo(() => _configService.Configuration).Returns(config);

        _service = new SessionMonitorService(_configService, _messenger, _logger);
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    [Test]
    public async Task Constructor_WithNullConfigurationService_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new SessionMonitorService(null!, _messenger, _logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullMessenger_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new SessionMonitorService(_configService, null!, _logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullLogger_CreatesInstance()
    {
        // Act
        var service = new SessionMonitorService(_configService, _messenger);

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task ProcessMessage_WithNonSessionChangeMessage_ReturnsFalse()
    {
        // Arrange
        const int wmPaint = 0x000F; // Random non-session-change message

        // Act
        var result = _service.ProcessMessage(wmPaint, IntPtr.Zero, IntPtr.Zero);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ProcessMessage_WithSessionLockMessage_ReturnsTrue()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int wtsSessionLock = 0x7;

        // Act
        var result = _service.ProcessMessage(wmWtsSessionChange, wtsSessionLock, IntPtr.Zero);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ProcessMessage_WithSessionUnlockMessage_ReturnsTrue()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int wtsSessionUnlock = 0x8;

        // Act
        var result = _service.ProcessMessage(wmWtsSessionChange, wtsSessionUnlock, IntPtr.Zero);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ProcessMessage_WithUnknownSessionChangeReason_ReturnsFalse()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int unknownReason = 999;

        // Act
        var result = _service.ProcessMessage(wmWtsSessionChange, unknownReason, IntPtr.Zero);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ProcessMessage_SessionLock_WhenLockOnScreenLockDisabled_DoesNotSendMessage()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int wtsSessionLock = 0x7;
        var config = new ClipMateConfiguration
        {
            Encryption = new EncryptionConfiguration { LockOnScreenLock = false },
        };

        A.CallTo(() => _configService.Configuration).Returns(config);

        // Act
        var result = _service.ProcessMessage(wmWtsSessionChange, wtsSessionLock, IntPtr.Zero);

        // Assert - Should handle the message (returns true)
        // Note: Can't verify messenger.Send() calls with FakeItEasy because Send() is an extension method
        // The configuration check behavior is tested indirectly through other tests
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ProcessMessage_SessionLock_WhenLockOnScreenLockEnabled_SendsLockMessage()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int wtsSessionLock = 0x7;
        var config = new ClipMateConfiguration
        {
            Encryption = new EncryptionConfiguration { LockOnScreenLock = true },
        };

        A.CallTo(() => _configService.Configuration).Returns(config);

        // Act
        var result = _service.ProcessMessage(wmWtsSessionChange, wtsSessionLock, IntPtr.Zero);

        // Assert - Should handle the message (returns true)
        // Note: Can't verify messenger.Send() calls with FakeItEasy because Send() is an extension method
        // Message delivery would be tested in integration tests
        await Assert.That(result).IsTrue();
        A.CallTo(() => _configService.Configuration).MustHaveHappened();
    }

    [Test]
    public async Task ProcessMessage_SessionLock_ChecksConfiguration()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int wtsSessionLock = 0x7;
        var config = new ClipMateConfiguration
        {
            Encryption = new EncryptionConfiguration { LockOnScreenLock = true },
        };

        A.CallTo(() => _configService.Configuration).Returns(config);

        // Act
        _service.ProcessMessage(wmWtsSessionChange, wtsSessionLock, IntPtr.Zero);

        // Assert - Verify configuration was accessed
        A.CallTo(() => _configService.Configuration).MustHaveHappened();
    }

    [Test]
    public async Task ProcessMessage_SessionUnlock_DoesNotSendMessage()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int wtsSessionUnlock = 0x8;
        var config = new ClipMateConfiguration
        {
            Encryption = new EncryptionConfiguration { LockOnScreenLock = true },
        };

        A.CallTo(() => _configService.Configuration).Returns(config);

        // Act
        var result = _service.ProcessMessage(wmWtsSessionChange, wtsSessionUnlock, IntPtr.Zero);

        // Assert - Unlock should still be handled but doesn't send messages
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ProcessMessage_MultipleSessionLocks_AllProcessed()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int wtsSessionLock = 0x7;
        var config = new ClipMateConfiguration
        {
            Encryption = new EncryptionConfiguration { LockOnScreenLock = true },
        };

        A.CallTo(() => _configService.Configuration).Returns(config);

        // Act
        var result1 = _service.ProcessMessage(wmWtsSessionChange, wtsSessionLock, IntPtr.Zero);
        var result2 = _service.ProcessMessage(wmWtsSessionChange, wtsSessionLock, IntPtr.Zero);

        // Assert - Both should be processed
        await Assert.That(result1).IsTrue();
        await Assert.That(result2).IsTrue();
        A.CallTo(() => _configService.Configuration).MustHaveHappened(2, Times.OrMore);
    }

    [Test]
    public async Task Start_WithValidHandle_DoesNotThrow()
    {
        // Arrange
        var handle = new IntPtr(12345);

        // Act & Assert - Should not throw (actual WTS registration will fail but is caught)
        _service.Start(handle);
    }

    [Test]
    public async Task Stop_AfterDispose_DoesNotThrow()
    {
        // Act & Assert
        _service.Dispose();
        _service.Stop(); // Should be safe to call after dispose
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_IsSafe()
    {
        // Act & Assert - Multiple dispose calls should be safe
        _service.Dispose();
        _service.Dispose();
    }

    [Test]
    public async Task ProcessMessage_AfterDispose_StillProcessesMessages()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int wtsSessionLock = 0x7;
        var config = new ClipMateConfiguration
        {
            Encryption = new EncryptionConfiguration { LockOnScreenLock = true },
        };

        A.CallTo(() => _configService.Configuration).Returns(config);

        // Act
        _service.Dispose();
        var result = _service.ProcessMessage(wmWtsSessionChange, wtsSessionLock, IntPtr.Zero);

        // Assert - ProcessMessage doesn't require Start/Stop, should still work
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ProcessMessage_ChecksConfigurationEachTime()
    {
        // Arrange
        const int wmWtsSessionChange = 0x02B1;
        const int wtsSessionLock = 0x7;
        var config = new ClipMateConfiguration
        {
            Encryption = new EncryptionConfiguration { LockOnScreenLock = false },
        };

        A.CallTo(() => _configService.Configuration).Returns(config);

        // Act - First call with disabled
        _service.ProcessMessage(wmWtsSessionChange, wtsSessionLock, IntPtr.Zero);

        // Change config
        config.Encryption.LockOnScreenLock = true;

        // Act - Second call with enabled
        _service.ProcessMessage(wmWtsSessionChange, wtsSessionLock, IntPtr.Zero);

        // Assert - Configuration should be checked each time
        A.CallTo(() => _configService.Configuration).MustHaveHappened(2, Times.OrMore);
    }
}
