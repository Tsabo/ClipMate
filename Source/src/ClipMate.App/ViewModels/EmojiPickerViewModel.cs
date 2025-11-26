using System.Collections.ObjectModel;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emoji.Wpf;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the custom emoji picker with search, categories, and recently used tracking.
/// </summary>
public partial class EmojiPickerViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;

    [ObservableProperty]
    private ObservableCollection<EmojiData.Emoji> _displayedEmojis = new();

    [ObservableProperty]
    private ObservableCollection<EmojiData.Emoji> _recentEmojis = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private EmojiData.Group? _selectedCategory;

    [ObservableProperty]
    private string? _selectedEmoji;

    public EmojiPickerViewModel(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        LoadRecentEmojis();

        // Default to first category (usually Smileys & Emotion)
        _selectedCategory = EmojiData.AllGroups.FirstOrDefault();
        UpdateDisplayedEmojis();
    }

    public IEnumerable<EmojiData.Group> Categories => EmojiData.AllGroups;

    [RelayCommand]
    private void SelectEmoji(EmojiData.Emoji emoji)
    {
        SelectedEmoji = emoji.Text;
        TrackEmojiUsage(emoji.Text);
    }

    [RelayCommand]
    private void Ok()
    {
        // Command for OK button - window will handle DialogResult
    }

    [RelayCommand]
    private void SelectCategory(EmojiData.Group category)
    {
        SelectedCategory = category;
        SearchText = string.Empty;
        UpdateDisplayedEmojis();
    }

    partial void OnSearchTextChanged(string value) => UpdateDisplayedEmojis();

    partial void OnSelectedCategoryChanged(EmojiData.Group? value) => UpdateDisplayedEmojis();

    private void UpdateDisplayedEmojis()
    {
        IEnumerable<EmojiData.Emoji> emojis;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            // Search across all emojis
            var searchLower = SearchText.ToLowerInvariant();
            emojis = EmojiData.AllEmoji
                .Where(p => p.Name.ToLowerInvariant().Contains(searchLower))
                .Take(64); // Limit search results
        }
        else if (SelectedCategory != null)
        {
            // Show emojis from selected category
            emojis = SelectedCategory.EmojiList;
        }
        else
            emojis = Enumerable.Empty<EmojiData.Emoji>();

        DisplayedEmojis = new ObservableCollection<EmojiData.Emoji>(emojis);
    }

    private void LoadRecentEmojis()
    {
        var recentEmojiTexts = _configurationService.Configuration.RecentEmojis
            .OrderByDescending(p => p.LastUsed)
            .ThenByDescending(p => p.UseCount)
            .Take(24)
            .Select(p => p.Emoji)
            .ToList();

        var recentEmojiObjects = recentEmojiTexts
            .Select(p => EmojiData.LookupByText.TryGetValue(p, out var emoji)
                ? emoji
                : null)
            .Where(p => p != null)
            .Cast<EmojiData.Emoji>()
            .ToList();

        RecentEmojis = new ObservableCollection<EmojiData.Emoji>(recentEmojiObjects);
    }

    private void TrackEmojiUsage(string emojiText)
    {
        var config = _configurationService.Configuration;
        var existing = config.RecentEmojis.FirstOrDefault(r => r.Emoji == emojiText);

        if (existing != null)
        {
            existing.LastUsed = DateTime.Now;
            existing.UseCount++;
        }
        else
        {
            config.RecentEmojis.Add(new RecentEmoji
            {
                Emoji = emojiText,
                LastUsed = DateTime.Now,
                UseCount = 1,
            });
        }

        // Keep only last 50 recent emojis
        if (config.RecentEmojis.Count > 50)
        {
            var toRemove = config.RecentEmojis
                .OrderBy(p => p.LastUsed)
                .ThenBy(p => p.UseCount)
                .Take(config.RecentEmojis.Count - 50)
                .ToList();

            foreach (var item in toRemove)
                config.RecentEmojis.Remove(item);
        }

        _ = _configurationService.SaveAsync();
        LoadRecentEmojis();
    }
}
