using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using DevExpress.Xpf.Grid;

namespace ClipMate.App.Helpers;

/// <summary>
/// Attached property helper to sync GridControl SelectedItems with a ViewModel collection.
/// Enables proper two-way synchronization across multiple views.
/// </summary>
public static class GridControlSelectionSync
{
    private static readonly Dictionary<GridControl, bool> _isUpdating = new();

    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItems",
            typeof(IList),
            typeof(GridControlSelectionSync),
            new PropertyMetadata(null, OnSelectedItemsChanged));

    public static IList? GetSelectedItems(DependencyObject obj) => (IList?)obj.GetValue(SelectedItemsProperty);

    public static void SetSelectedItems(DependencyObject obj, IList? value) => obj.SetValue(SelectedItemsProperty, value);

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GridControl grid)
            return;

        // Unsubscribe from old collection
        if (e.OldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= (s, args) => OnViewModelCollectionChanged(grid, args);

        // Subscribe to new collection
        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += (s, args) => OnViewModelCollectionChanged(grid, args);

            // Subscribe to grid selection changes
            grid.SelectionChanged -= OnGridSelectionChanged;
            grid.SelectionChanged += OnGridSelectionChanged;

            // Initial sync from ViewModel to Grid
            SyncViewModelToGrid(grid, (IList)e.NewValue);
        }
    }

    private static void OnViewModelCollectionChanged(GridControl grid, NotifyCollectionChangedEventArgs e)
    {
        if (_isUpdating.TryGetValue(grid, out var updating) && updating)
            return;

        var viewModelCollection = GetSelectedItems(grid);
        if (viewModelCollection == null)
            return;

        grid.Dispatcher.BeginInvoke(() =>
        {
            _isUpdating[grid] = true;
            try
            {
                SyncViewModelToGrid(grid, viewModelCollection);
            }
            finally
            {
                _isUpdating[grid] = false;
            }
        }, DispatcherPriority.DataBind);
    }

    private static void OnGridSelectionChanged(object? sender, GridSelectionChangedEventArgs e)
    {
        if (sender is not GridControl grid)
            return;

        if (_isUpdating.TryGetValue(grid, out var updating) && updating)
            return;

        var viewModelCollection = GetSelectedItems(grid);
        if (viewModelCollection == null)
            return;

        grid.Dispatcher.BeginInvoke(() =>
        {
            _isUpdating[grid] = true;
            try
            {
                viewModelCollection.Clear();
                foreach (var item in grid.SelectedItems)
                    viewModelCollection.Add(item);
            }
            finally
            {
                _isUpdating[grid] = false;
            }
        }, DispatcherPriority.DataBind);
    }

    private static void SyncViewModelToGrid(GridControl grid, IList viewModelCollection)
    {
        grid.SelectedItems.Clear();
        foreach (var item in viewModelCollection)
            grid.SelectedItems.Add(item);
    }
}
