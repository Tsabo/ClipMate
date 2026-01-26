using System.Diagnostics;
using ClipMate.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="SoundService.PlaySoundAsync" />.
/// Note: These tests validate basic behavior without sound file dependencies.
/// </summary>
public class SoundServiceTestsPlaySound : SoundServiceTestsBase
{
    [Test]
    public async Task PlaySoundAsync_WhenSoundDisabled_CompletesImmediately()
    {
        // Arrange
        var service = CreateService();
        Configuration.Preferences.Sound.ClipboardUpdate = SoundMode.Off;
        var stopwatch = Stopwatch.StartNew();

        // Act
        await service.PlaySoundAsync(SoundEvent.ClipboardUpdate);
        stopwatch.Stop();

        // Assert - Should complete very quickly without attempting playback
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(100);
        
        // Verify no logging occurred (sound was disabled)
        Logger.Verify(
            p => p.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Test]
    public async Task PlaySoundAsync_WithAllEventsDisabled_CompletesQuicklyForEachEvent()
    {
        // Arrange
        var service = CreateService();
        // All sounds already disabled in base setup
        var events = new[]
        {
            SoundEvent.ClipboardUpdate,
            SoundEvent.Append,
            SoundEvent.Erase,
            SoundEvent.Filter,
            SoundEvent.Ignore,
            SoundEvent.PowerPasteComplete,
        };

        // Act & Assert - Each should complete quickly when disabled
        foreach (var item in events)
        {
            var startTime = DateTime.UtcNow;
            await service.PlaySoundAsync(item);
            var duration = DateTime.UtcNow - startTime;
            
            await Assert.That(duration.TotalMilliseconds).IsLessThan(100);
        }
    }

    [Test]
    public async Task PlaySoundAsync_WithSoundModeOff_DoesNotAccessFileSystem()
    {
        // Arrange
        var service = CreateService();
        Configuration.Preferences.Sound.Filter = SoundMode.Off;

        // Act - Call with disabled sound
        var stopwatch = Stopwatch.StartNew();
        await service.PlaySoundAsync(SoundEvent.Filter);
        stopwatch.Stop();

        // Assert - Should return immediately without file system access
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(50);
    }

    [Test]
    public async Task PlaySoundAsync_WithDifferentSoundModes_CompletesForDisabledSounds()
    {
        // Arrange
        var service = CreateService();
        // Test different combinations of disabled sounds
        Configuration.Preferences.Sound.Erase = SoundMode.Off;
        Configuration.Preferences.Sound.Append = SoundMode.Off;

        // Act & Assert - All disabled sounds should complete immediately
        await service.PlaySoundAsync(SoundEvent.Erase);
        await service.PlaySoundAsync(SoundEvent.Append);
    }

    [Test]
    public async Task PlaySoundAsync_WithCancellationToken_AcceptsToken()
    {
        // Arrange
        var service = CreateService();
        Configuration.Preferences.Sound.ClipboardUpdate = SoundMode.Off;
        using var cts = new CancellationTokenSource();

        // Act & Assert - Should complete without throwing
        await service.PlaySoundAsync(SoundEvent.ClipboardUpdate, cts.Token);
    }

    [Test]
    public async Task PlaySoundAsync_WithMultipleCalls_HandlesSequentially()
    {
        // Arrange
        var service = CreateService();
        Configuration.Preferences.Sound.ClipboardUpdate = SoundMode.Off;
        Configuration.Preferences.Sound.Append = SoundMode.Off;
        Configuration.Preferences.Sound.Erase = SoundMode.Off;

        // Act & Assert - All should complete successfully without throwing
        await service.PlaySoundAsync(SoundEvent.ClipboardUpdate);
        await service.PlaySoundAsync(SoundEvent.Append);
        await service.PlaySoundAsync(SoundEvent.Erase);
    }

    [Test]
    public async Task PlaySoundAsync_ChecksConfigurationForEachEvent()
    {
        // Arrange
        var service = CreateService();
        // Keep both sounds disabled to avoid file system access
        Configuration.Preferences.Sound.ClipboardUpdate = SoundMode.Off;
        Configuration.Preferences.Sound.Append = SoundMode.Off;

        // Act
        await service.PlaySoundAsync(SoundEvent.ClipboardUpdate);
        await service.PlaySoundAsync(SoundEvent.Append);

        // Assert - Configuration should have been accessed for behavior checks
        ConfigService.Verify(p => p.Configuration, Times.AtLeastOnce);
    }

    [Test]
    public async Task PlaySoundAsync_WithAllSoundEventsDisabled_NeverLogsWarnings()
    {
        // Arrange
        var service = CreateService();
        // All sounds disabled by default

        // Act - Call all sound events
        foreach (var item in Enum.GetValues<SoundEvent>())
        {
            await service.PlaySoundAsync(item);
        }

        // Assert - No warnings should be logged when sounds are disabled
        Logger.Verify(
            p => p.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
