using ClipMate.Core.Models;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class ApplicationFilterServiceTests
{
    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithNoActiveDatabase_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(string.Empty);

        // Act
        var result = await service.ShouldFilterAsync("notepad.exe", "Untitled - Notepad");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithNoEnabledFilters_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns([]);

        // Act
        var result = await service.ShouldFilterAsync("notepad.exe", "Document");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithMatchingProcessName_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var filter = CreateTestFilter("Block Notepad", "notepad.exe");
        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns([filter]);

        // Act
        var result = await service.ShouldFilterAsync("notepad.exe", "Some title");

        // Assert
        await Assert.That(result).IsTrue();
        A.CallTo(() => _mockSoundService.PlaySoundAsync(SoundEvent.Filter, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithMatchingWindowTitle_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var filter = CreateTestFilter("Block Passwords", windowTitlePattern: "*password*");
        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns([filter]);

        // Act
        var result = await service.ShouldFilterAsync("chrome.exe", "Enter Password - Google Chrome");

        // Assert
        await Assert.That(result).IsTrue();
        A.CallTo(() => _mockSoundService.PlaySoundAsync(SoundEvent.Filter, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithBothProcessAndTitleMatching_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var filter = CreateTestFilter("Block Notepad Passwords",
            "notepad.exe",
            "*password*");

        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns([filter]);

        // Act
        var result = await service.ShouldFilterAsync("notepad.exe", "password.txt - Notepad");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithOnlyProcessMatching_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var filter = CreateTestFilter("Block Notepad Passwords",
            "notepad.exe",
            "*password*");

        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns([filter]);

        // Act
        var result = await service.ShouldFilterAsync("notepad.exe", "document.txt - Notepad");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithOnlyTitleMatching_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var filter = CreateTestFilter("Block Notepad Passwords",
            "notepad.exe",
            "*password*");

        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns([filter]);

        // Act
        var result = await service.ShouldFilterAsync("chrome.exe", "password.txt");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithWildcardPattern_MatchesCorrectly()
    {
        // Arrange
        var service = CreateService();
        var filter = CreateTestFilter("Block Chrome", "chrome*.exe");
        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns([filter]);

        // Act
        var result1 = await service.ShouldFilterAsync("chrome.exe", "Title");
        var result2 = await service.ShouldFilterAsync("chrome-beta.exe", "Title");
        var result3 = await service.ShouldFilterAsync("chromium.exe", "Title");

        // Assert
        await Assert.That(result1).IsTrue();
        await Assert.That(result2).IsTrue();
        await Assert.That(result3).IsFalse(); // chromium doesn't match chrome*
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_IsCaseInsensitive()
    {
        // Arrange
        var service = CreateService();
        var filter = CreateTestFilter("Block Notepad", "NOTEPAD.EXE");
        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns([filter]);

        // Act
        var result = await service.ShouldFilterAsync("notepad.exe", "Title");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithException_ReturnsFalseAndDoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await service.ShouldFilterAsync("notepad.exe", "Title");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("ShouldFilterAsync")]
    public async Task ShouldFilterAsync_WithNullProcessAndTitle_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var filter = CreateTestFilter("Test", "notepad.exe");
        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns([filter]);

        // Act
        var result = await service.ShouldFilterAsync(null, null);

        // Assert
        await Assert.That(result).IsFalse();
    }
}
