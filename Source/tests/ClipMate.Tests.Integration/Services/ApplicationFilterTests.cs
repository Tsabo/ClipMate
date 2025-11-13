using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for application filter exclusion.
/// Verifies that clips from filtered applications are correctly excluded.
/// </summary>
public class ApplicationFilterTests : IntegrationTestBase
{
    [Fact]
    public async Task ShouldMatchFilter_WithExactProcessName_ShouldReturnTrue()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Notepad", "notepad.exe", null, true);

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync("notepad.exe", null);

        // Assert
        shouldFilter.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldMatchFilter_WithDifferentProcessName_ShouldReturnFalse()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Notepad", "notepad.exe", null, true);

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync("chrome.exe", null);

        // Assert
        shouldFilter.ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldMatchFilter_WithWindowTitlePattern_ShouldReturnTrue()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Password Windows", null, "*Password*", true);

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync(null, "Change Password - Windows Security");

        // Assert
        shouldFilter.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldMatchFilter_WithNonMatchingWindowTitle_ShouldReturnFalse()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Password Windows", null, "*Password*", true);

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync(null, "Notepad - Untitled");

        // Assert
        shouldFilter.ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldMatchFilter_WithDisabledFilter_ShouldReturnFalse()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Notepad", "notepad.exe", null, false); // Disabled

        // Act
        var shouldFilter = await filterService.ShouldFilterAsync("notepad.exe", null);

        // Assert
        shouldFilter.ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldMatchFilter_WithBothProcessAndTitle_ShouldMatchBoth()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block Notepad Passwords", "notepad.exe", "*password*", true);

        // Act - Both match
        var shouldFilter1 = await filterService.ShouldFilterAsync("notepad.exe", "password.txt - Notepad");
        
        // Act - Only process matches
        var shouldFilter2 = await filterService.ShouldFilterAsync("notepad.exe", "document.txt - Notepad");
        
        // Act - Only title matches
        var shouldFilter3 = await filterService.ShouldFilterAsync("chrome.exe", "password manager");

        // Assert
        shouldFilter1.ShouldBeTrue();   // Both match - should filter
        shouldFilter2.ShouldBeFalse();  // Only process matches - should not filter
        shouldFilter3.ShouldBeFalse();  // Only title matches - should not filter
    }

    [Fact]
    public async Task GetAllFilters_ShouldReturnAllFilters()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        
        await filterService.CreateFilterAsync("Filter 1", "app1.exe", null, true);
        await filterService.CreateFilterAsync("Filter 2", "app2.exe", null, true);

        // Act
        var filters = await filterService.GetAllFiltersAsync();

        // Assert
        filters.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WildcardPattern_ShouldSupportAsterisk()
    {
        // Arrange
        var filterService = CreateApplicationFilterService();
        await filterService.CreateFilterAsync("Block All Passwords", null, "*password*", true);

        // Act
        var match1 = await filterService.ShouldFilterAsync(null, "my password file");
        var match2 = await filterService.ShouldFilterAsync(null, "PASSWORD");
        var match3 = await filterService.ShouldFilterAsync(null, "passwords.txt");
        var match4 = await filterService.ShouldFilterAsync(null, "document.txt");

        // Assert
        match1.ShouldBeTrue();
        match2.ShouldBeTrue();  // Case-insensitive
        match3.ShouldBeTrue();
        match4.ShouldBeFalse();
    }

    /// <summary>
    /// Creates an application filter service instance for testing.
    /// </summary>
    private IApplicationFilterService CreateApplicationFilterService()
    {
        var repository = new ApplicationFilterRepository(DbContext);
        var logger = Mock.Of<ILogger<ApplicationFilterService>>();
        return new ApplicationFilterService(repository, logger);
    }
}
