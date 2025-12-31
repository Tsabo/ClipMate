using System.Collections.Specialized;
using ClipMate.App.ViewModels;
using DevExpress.Xpf.Grid;
using Application = System.Windows.Application;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for displaying application event log.
/// </summary>
public partial class EventLogDialog
{
    private readonly EventLogViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventLogDialog" /> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public EventLogDialog(EventLogViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        InitializeComponent();

        // Subscribe to collection changes for auto-scroll
        if (_viewModel.Events is INotifyCollectionChanged notifyCollection)
            notifyCollection.CollectionChanged += OnEventsCollectionChanged;
    }

    private void OnEventsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel.AutoScroll && e.Action == NotifyCollectionChangedAction.Add)
        {
            // Scroll to the last item
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (EventGrid.View is not TableView tableView || _viewModel.Events.Count <= 0)
                    return;

                tableView.FocusedRowHandle = _viewModel.Events.Count - 1;
                tableView.ScrollIntoView(_viewModel.Events.Count - 1);
            });
        }
    }
}
