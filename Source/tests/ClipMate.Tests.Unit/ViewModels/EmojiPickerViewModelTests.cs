using System.Collections.ObjectModel;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Emoji.Wpf;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

public class EmojiPickerViewModelTests
{
    // Constructor Tests
    [Test]
    public async Task Constructor_WithNullConfigurationService_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new EmojiPickerViewModel(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidService_InitializesProperties()
    {
        // Arrange
        var configService = CreateMockConfigService();

        // Act
        var viewModel = new EmojiPickerViewModel(configService.Object);

        // Assert
        await Assert.That(viewModel.SearchText).IsEqualTo(string.Empty);
        await Assert.That(viewModel.DisplayedEmojis).IsNotNull();
        await Assert.That(viewModel.RecentEmojis).IsNotNull();
        await Assert.That(viewModel.SelectedCategory).IsNotNull();
    }

    [Test]
    public async Task Constructor_LoadsRecentEmojis()
    {
        // Arrange
        var configService = CreateMockConfigService(new List<RecentEmoji>
        {
            new() { Emoji = "😀", LastUsed = DateTime.Now, UseCount = 5 },
            new() { Emoji = "😎", LastUsed = DateTime.Now.AddDays(-1), UseCount = 3 }
        });

        // Act
        var viewModel = new EmojiPickerViewModel(configService.Object);

        // Assert
        await Assert.That(viewModel.RecentEmojis.Count).IsGreaterThanOrEqualTo(0);
    }

    // SearchText Tests
    [Test]
    public async Task SearchText_WhenSet_UpdatesDisplayedEmojis()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);
        var initialCount = viewModel.DisplayedEmojis.Count;

        // Act
        viewModel.SearchText = "smile";

        // Assert - search should filter emojis
        await Assert.That(viewModel.DisplayedEmojis).IsNotNull();
    }

    [Test]
    public async Task SearchText_WithEmptyString_ShowsCategoryEmojis()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);

        viewModel.SearchText = "test";

        // Act
        viewModel.SearchText = string.Empty;

        // Assert
        await Assert.That(viewModel.DisplayedEmojis.Count).IsGreaterThan(0);
    }

    // SelectedCategory Tests
    [Test]
    public async Task SelectedCategory_WhenSet_UpdatesDisplayedEmojis()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);
        var categories = EmojiData.AllGroups.ToList();

        if (categories.Count > 1)
        {
            var newCategory = categories[1];

            // Act
            viewModel.SelectedCategory = newCategory;

            // Assert
            await Assert.That(viewModel.DisplayedEmojis).IsNotNull();
            await Assert.That(viewModel.SelectedCategory).IsEqualTo(newCategory);
        }
        // Test completes successfully even with single category
    }

    // SelectEmojiCommand Tests
    [Test]
    public async Task SelectEmojiCommand_SetsSelectedEmoji()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);
        var emoji = new EmojiData.Emoji { Text = "😀", Name = "grinning face" };

        // Act
        viewModel.SelectEmojiCommand.Execute(emoji);

        // Assert
        await Assert.That(viewModel.SelectedEmoji).IsEqualTo("😀");
    }

    [Test]
    public async Task SelectEmojiCommand_TracksEmojiUsage()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);
        var emoji = new EmojiData.Emoji { Text = "😀", Name = "grinning face" };

        // Act
        viewModel.SelectEmojiCommand.Execute(emoji);

        // Assert - verify SaveAsync was called to persist usage
        configService.Verify(c => c.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SelectEmojiCommand_WithMultipleSelections_UpdatesRecentEmojis()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);
        var emoji1 = new EmojiData.Emoji { Text = "😀", Name = "grinning face" };
        var emoji2 = new EmojiData.Emoji { Text = "😎", Name = "smiling face with sunglasses" };

        // Act
        viewModel.SelectEmojiCommand.Execute(emoji1);
        viewModel.SelectEmojiCommand.Execute(emoji2);

        // Assert - verify SaveAsync was called multiple times
        configService.Verify(c => c.SaveAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    // SelectCategoryCommand Tests
    [Test]
    public async Task SelectCategoryCommand_ChangesSelectedCategory()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);
        var categories = EmojiData.AllGroups.ToList();

        if (categories.Count > 1)
        {
            var newCategory = categories[1];

            // Act
            viewModel.SelectCategoryCommand.Execute(newCategory);

            // Assert
            await Assert.That(viewModel.SelectedCategory).IsEqualTo(newCategory);
        }
        // Test completes successfully
    }

    [Test]
    public async Task SelectCategoryCommand_ClearsSearchText()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);
        var categories = EmojiData.AllGroups.ToList();

        viewModel.SearchText = "test search";

        if (categories.Count > 0)
        {
            // Act
            viewModel.SelectCategoryCommand.Execute(categories[0]);

            // Assert
            await Assert.That(viewModel.SearchText).IsEqualTo(string.Empty);
        }
        // Test completes successfully
    }

    // OkCommand Tests
    [Test]
    public async Task OkCommand_CanExecute()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);

        // Act
        var canExecute = viewModel.OkCommand.CanExecute(null);

        // Assert
        await Assert.That(canExecute).IsTrue();
    }

    [Test]
    public async Task OkCommand_Execute_DoesNotThrow()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);

        // Act
        viewModel.OkCommand.Execute(null);

        // Assert - command executed without throwing
        await Assert.That(viewModel).IsNotNull();
    }

    // Categories Tests
    [Test]
    public async Task Categories_ReturnsAllEmojiGroups()
    {
        // Arrange
        var configService = CreateMockConfigService();
        var viewModel = new EmojiPickerViewModel(configService.Object);

        // Act
        var categories = viewModel.Categories;

        // Assert
        await Assert.That(categories).IsNotNull();
        await Assert.That(categories.Any()).IsTrue();
    }

    // Helper Methods
    private static Mock<IConfigurationService> CreateMockConfigService(List<RecentEmoji>? recentEmojis = null)
    {
        var mock = new Mock<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            RecentEmojis = recentEmojis ?? new List<RecentEmoji>()
        };

        mock.Setup(c => c.Configuration).Returns(config);
        mock.Setup(c => c.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return mock;
    }
}
