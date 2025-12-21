using System.Collections.ObjectModel;
using System.Diagnostics;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Views;

/// <summary>
/// Reusable ClipList grid component that displays clipboard clips.
/// Supports dual ClipList scenarios where multiple instances can be shown simultaneously.
/// </summary>
public partial class ClipListView
{
    /// <summary>
    /// Dependency property for the collection of clips to display
    /// </summary>
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(
            nameof(Items),
            typeof(ObservableCollection<Clip>),
            typeof(ClipListView),
            new PropertyMetadata(null, OnItemsChanged));

    /// <summary>
    /// Dependency property for the currently selected clip
    /// </summary>
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(Clip),
            typeof(ClipListView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Dependency property for the header text displayed above the grid
    /// </summary>
    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(
            nameof(HeaderText),
            typeof(string),
            typeof(ClipListView),
            new PropertyMetadata("Clips"));

    /// <summary>
    /// Dependency property for the selected items collection
    /// </summary>
    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(
            nameof(SelectedItems),
            typeof(ObservableCollection<Clip>),
            typeof(ClipListView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

    /// <summary>
    /// Routed event for selection changes
    /// </summary>
    public static readonly RoutedEvent SelectionChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SelectionChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(ClipListView));

    private readonly IMessenger _messenger;

    public ClipListView()
    {
        InitializeComponent();

        // Get messenger from DI container
        var app = (App)Application.Current;
        _messenger = (IMessenger)app.ServiceProvider.GetService(typeof(IMessenger))!;
    }

    /// <summary>
    /// Gets or sets the collection of clips to display
    /// </summary>
    public ObservableCollection<Clip> Items
    {
        get => (ObservableCollection<Clip>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection of selected clips
    /// </summary>
    public ObservableCollection<Clip> SelectedItems
    {
        get => (ObservableCollection<Clip>)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected clip
    /// </summary>
    public Clip? SelectedItem
    {
        get => (Clip?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Gets or sets the header text displayed above the grid
    /// </summary>
    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Selection sync is now handled by GridControlSelectionSync attached property
    }

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newCollection = e.NewValue as ObservableCollection<Clip>;
        Debug.WriteLine($"ClipListView.Items changed: Count={newCollection?.Count ?? 0}");
    }

    /// <summary>
    /// Event raised when the selected clip changes
    /// </summary>
    public event RoutedEventHandler SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>
    /// Handles the CurrentItemChanged event from the DevExpress GridControl
    /// </summary>
    private void ClipDataGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        // Update the SelectedItem property
        var newClip = e.NewItem as Clip;
        Debug.WriteLine($"[ClipListView] CurrentItemChanged - ClipId: {newClip?.Id}, Title: {newClip?.DisplayTitle}");
        SelectedItem = newClip;

        // Raise the SelectionChanged routed event
        RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));
    }

    private async void ClipProperties_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null)
            return;

        var dialog = new ClipPropertiesDialog();
        var app = (App)Application.Current;
        if (app.ServiceProvider.GetService(typeof(ClipPropertiesViewModel)) is not ClipPropertiesViewModel viewModel)
            return;

        await viewModel.LoadClipAsync(SelectedItem);
        dialog.DataContext = viewModel;
        dialog.Owner = Application.Current.GetDialogOwner();
        dialog.ShowDialog();
    }

    private async void PasteNow_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null)
            return;

        // Get the parent ViewModel to access SetClipboardContentAsync
        var app = (App)Application.Current;
        if (app.ServiceProvider.GetService(typeof(ClipListViewModel)) is ClipListViewModel viewModel)
            await viewModel.SetClipboardContentAsync(SelectedItem);
    }

    private void CreateNewClip_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement create new clip
    }

    private void ViewClip_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement view clip
    }

    private void ExportClips_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement export clips
    }

    private void ChangeTitle_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement change title
    }

    private void DeleteItems_Click(object sender, RoutedEventArgs e)
    {
        // Send delete event with IDs of currently selected clips
        var clipIds = SelectedItems.OfType<Clip>().Select(c => c.Id).ToList();
        _messenger.Send(new DeleteClipsRequestedEvent(clipIds));
    }

    private void OpenSourceUrl_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem?.SourceUrl != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = SelectedItem.SourceUrl,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open URL: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Handles the start of a record drag operation to enable dragging clips to collection tree.
    /// Adds Clip objects to the drag data so they can be dropped on collections.
    /// </summary>
    private void TableView_StartRecordDrag(object sender, StartRecordDragEventArgs e)
    {
        // Get the dragged clips
        var draggedClips = e.Records.OfType<Clip>().ToList();
        if (draggedClips.Any())
        {
            // Add clips to drag data using a custom format
            e.Data.SetData(typeof(Clip[]), draggedClips.ToArray());
        }
    }
}
