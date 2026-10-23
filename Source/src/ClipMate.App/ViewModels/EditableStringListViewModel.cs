using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// A list of strings on an options page that the user adds to, edits and deletes from, shown by
/// <see cref="Controls.EditableStringList" />.
/// </summary>
public partial class EditableStringListViewModel : ObservableObject
{
    private readonly Func<string, string?> _prompt;

    [ObservableProperty]
    private ObservableCollection<string> _items = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <param name="prompt">
    /// Asks the user for a value, starting from the one given (empty when adding); returns null when cancelled.
    /// </param>
    public EditableStringListViewModel(Func<string, string?> prompt)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
    }

    /// <summary>
    /// Replaces the items with <paramref name="items" />.
    /// </summary>
    public void Load(IEnumerable<string> items)
    {
        Items = new ObservableCollection<string>(items);
        SelectedIndex = -1;
    }

    /// <summary>
    /// Gets a copy of the items, for saving.
    /// </summary>
    public List<string> ToList() => [.. Items];

    [RelayCommand]
    private void Add()
    {
        var value = Prompt(string.Empty);
        if (value != null && !Items.Contains(value, StringComparer.OrdinalIgnoreCase))
            Items.Add(value);
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
            return;

        var index = SelectedIndex;
        var value = Prompt(Items[index]);
        if (value != null)
            Items[index] = value;
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Items.Count)
            Items.RemoveAt(SelectedIndex);
    }

    private string? Prompt(string current)
    {
        var value = _prompt(current)?.Trim();
        return string.IsNullOrEmpty(value)
            ? null
            : value;
    }
}
