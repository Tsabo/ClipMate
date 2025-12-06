using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for application filter exclusion.
/// Verifies that clips from filtered applications are correctly excluded.
/// </summary>
public class ApplicationFilterTests : IntegrationTestBase
{
    [Test]
    public async Task ShouldMatchFilter_WithExactProcessName_ShouldReturnTrue()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Notepad", "notepad.exe", null);

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync("notepad.exe", null);

        // Assert
        await Assert.That(shouldFilter).IsTrue();
    }

    [Test]
    public async Task ShouldMatchFilter_WithDifferentProcessName_ShouldReturnFalse()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Notepad", "notepad.exe", null);

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync("chrome.exe", null);

        // Assert
        await Assert.That(shouldFilter).IsFalse();
    }

    [Test]
    public async Task ShouldMatchFilter_WithWindowTitlePattern_ShouldReturnTrue()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Password Windows", null, "*Password*");

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync(null, "Change Password - Windows Security");

        // Assert
        await Assert.That(shouldFilter).IsTrue();
    }

    [Test]
    public async Task ShouldMatchFilter_WithNonMatchingWindowTitle_ShouldReturnFalse()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Password Windows", null, "*Password*");

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync(null, "Notepad - Untitled");

        // Assert
        await Assert.That(shouldFilter).IsFalse();
    }

    [Test]
    public async Task ShouldMatchFilter_WithDisabledFilter_ShouldReturnFalse()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Notepad", "notepad.exe", null, false); // Disabled

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync("notepad.exe", null);

        // Assert
        await Assert.That(shouldFilter).IsFalse();
    }

    [Test]
    public async Task ShouldMatchFilter_WithBothProcessAndTitle_ShouldMatchBoth()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Notepad Passwords", "notepad.exe", "*password*");

        // Act - Both match
        var shouldFilter1 = await filterService.ShouldFilterAsync("notepad.exe", "password.txt - Notepad");

        // Act - Only process matches
        var shouldFilter2 = await filterService.ShouldFilterAsync("notepad.exe", "document.txt - Notepad");

        // Act - Only title matches
        var shouldFilter3 = await filterService.ShouldFilterAsync("chrome.exe", "password manager");

        // Assert
        await Assert.That(shouldFilter1).IsTrue(); // Both match - should filter
        await Assert.That(shouldFilter2).IsFalse(); // Only process matches - should not filter
        await Assert.That(shouldFilter3).IsFalse(); // Only title matches - should not filter
    }

    [Test]
    public async Task GetAllFilters_ShouldReturnAllFilters()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();

        await filterService.CreateFilterAsync("Filter 1", "app1.exe", null);
        await filterService.CreateFilterAsync("Filter 2", "app2.exe", null);

        // Act
        var filters = await filterService.GetAllFiltersAsync();

        // Assert
        await Assert.That(filters.Count).IsEqualTo(2);
    }

    [Test]
    public async Task WildcardPattern_ShouldSupportAsterisk()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block All Passwords", null, "*password*");

        // Act
        var match1 = await filterService.ShouldFilterAsync(null, "my password file");
        var match2 = await filterService.ShouldFilterAsync(null, "PASSWORD");
        var match3 = await filterService.ShouldFilterAsync(null, "passwords.txt");
        var match4 = await filterService.ShouldFilterAsync(null, "document.txt");

        // Assert
        await Assert.That(match1).IsTrue();
        await Assert.That(match2).IsTrue(); // Case-insensitive
        await Assert.That(match3).IsTrue();
        await Assert.That(match4).IsFalse();
    }

    /// <summary>
    /// Creates an application filter service instance for testing.
    /// </summary>
    private IApplicationFilterService CreateApplicationFilterService()
    {
        var repository = new ApplicationFilterRepository(DbContext);
        var logger = Mock.Of<ILogger<ApplicationFilterService>>();
        var soundService = new Mock<ISoundService>();
        soundService.Setup(s => s.PlaySoundAsync(It.IsAny<SoundEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return new ApplicationFilterService(repository, soundService.Object, logger);
    }
}
