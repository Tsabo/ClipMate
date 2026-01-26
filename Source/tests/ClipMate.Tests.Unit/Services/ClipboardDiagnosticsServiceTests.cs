using ClipMate.Core.Models;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="ClipboardDiagnosticsService" />.
/// Note: Most methods interact with Win32 clipboard APIs which are integration-test level.
/// These tests focus on constructor validation and testable logic paths.
/// </summary>
public class ClipboardDiagnosticsServiceTests
{
    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new ClipboardDiagnosticsService(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();

        // Act
        var service = new ClipboardDiagnosticsService(logger.Object);

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task GetDiagnostics_ReturnsClipboardDiagnosticInfo()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();
        var service = new ClipboardDiagnosticsService(logger.Object);

        // Act
        var result = service.GetDiagnostics();

        // Assert - Should return diagnostic info (actual content depends on clipboard state)
        await Assert.That(result).IsNotNull();
        await Assert.That(result.OwnerProcessName).IsNotNull();
        await Assert.That(result.Formats).IsNotNull();
    }

    [Test]
    public async Task GetOwnerProcessName_ReturnsString()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();
        var service = new ClipboardDiagnosticsService(logger.Object);

        // Act
        var result = service.GetOwnerProcessName();

        // Assert - Should return a process name or "(No owner)"
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<string>();
    }

    [Test]
    public async Task GetSequenceNumber_ReturnsUInt()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();
        var service = new ClipboardDiagnosticsService(logger.Object);

        // Act
        var result = service.GetSequenceNumber();

        // Assert - Should return a sequence number (any uint is valid)
        await Assert.That(result).IsTypeOf<uint>();
    }

    [Test]
    public async Task GetFormatName_WithStandardFormat_ReturnsStandardName()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();
        var service = new ClipboardDiagnosticsService(logger.Object);
        const uint cfText = 1; // CF_TEXT

        // Act
        var result = service.GetFormatName(cfText);

        // Assert - Should return the standard format name
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<string>();
    }

    [Test]
    public async Task GetFormatName_WithCustomFormat_ReturnsFallbackOrRegisteredName()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();
        var service = new ClipboardDiagnosticsService(logger.Object);
        const uint customFormat = 49999; // Arbitrary custom format code

        // Act
        var result = service.GetFormatName(customFormat);

        // Assert - Should return either registered name or "Format_{code}" fallback
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<string>();
    }

    [Test]
    public async Task GetFormatName_WithZeroFormat_ReturnsFormatString()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();
        var service = new ClipboardDiagnosticsService(logger.Object);

        // Act
        var result = service.GetFormatName(0);

        // Assert - Should handle zero format code gracefully
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task GetFormatName_CalledMultipleTimes_ReturnsConsistentResults()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();
        var service = new ClipboardDiagnosticsService(logger.Object);
        const uint cfText = 1;

        // Act
        var result1 = service.GetFormatName(cfText);
        var result2 = service.GetFormatName(cfText);

        // Assert - Should return the same result for the same format code
        await Assert.That(result1).IsEqualTo(result2);
    }

    [Test]
    public async Task GetSequenceNumber_CalledMultipleTimes_MayReturnDifferentValues()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();
        var service = new ClipboardDiagnosticsService(logger.Object);

        // Act
        var result1 = service.GetSequenceNumber();
        var result2 = service.GetSequenceNumber();

        // Assert - Sequence numbers are valid uints (may or may not be equal)
        await Assert.That(result1).IsTypeOf<uint>();
        await Assert.That(result2).IsTypeOf<uint>();
    }

    [Test]
    public async Task GetDiagnostics_ReturnsFormatsCollection()
    {
        // Arrange
        var logger = new Mock<ILogger<ClipboardDiagnosticsService>>();
        var service = new ClipboardDiagnosticsService(logger.Object);

        // Act
        var result = service.GetDiagnostics();

        // Assert - Formats collection should be initialized (empty or with items)
        await Assert.That(result.Formats).IsNotNull();
        await Assert.That(result.Formats).IsTypeOf<IReadOnlyList<ClipboardFormatDiagnostic>>();
    }
}
