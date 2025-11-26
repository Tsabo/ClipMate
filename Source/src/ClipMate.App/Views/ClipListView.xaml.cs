using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
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
    /// Routed event for selection changes
    /// </summary>
    public static readonly RoutedEvent SelectionChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SelectionChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(ClipListView));

    public ClipListView()
    {
        InitializeComponent();
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

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ClipListView)d;
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
        SelectedItem = e.NewItem as Clip;

        // Raise the SelectionChanged routed event
        RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));
    }

    private async void ClipProperties_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null)
            return;

        var dialog = new ClipPropertiesDialog();
        var app = (App)Application.Current;
        if (app.ServiceProvider.GetService(typeof(ClipPropertiesViewModel)) is ClipPropertiesViewModel viewModel)
        {
            await viewModel.LoadClipAsync(SelectedItem);
            dialog.DataContext = viewModel;
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }
    }

    private void PasteNow_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement paste functionality
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
        // TODO: Implement delete
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
}
