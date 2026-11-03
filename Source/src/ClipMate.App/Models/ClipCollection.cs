using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ClipMate.Core.Models;

namespace ClipMate.App.Models;

/// <summary>
/// The clips a clip list shows, which can grow by a whole page at once: <see cref="AddRange" /> raises one
/// <see cref="NotifyCollectionChangedAction.Reset" /> for the page, so the grid rebuilds once instead of once per clip.
/// </summary>
public sealed class ClipCollection : ObservableCollection<Clip>
{
    /// <summary>
    /// Adds <paramref name="clips" /> at the end, raising one <see cref="NotifyCollectionChangedAction.Reset" />, or
    /// nothing when there are none.
    /// </summary>
    public void AddRange(IEnumerable<Clip> clips)
    {
        ArgumentNullException.ThrowIfNull(clips);
        CheckReentrancy();

        var countBefore = Items.Count;
        foreach (var item in clips)
            Items.Add(item);

        if (Items.Count == countBefore)
            return;

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
