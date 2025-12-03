using System.Windows;
using ClipMate.App.Views;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for Collection Properties dialog.
/// Supports Normal, Folder, Trashcan, and Virtual collection types.
/// </summary>
public partial class CollectionPropertiesViewModel : ObservableObject
{
    private readonly Collection _collection;
    private readonly IConfigurationService _configurationService;
    private readonly bool _isNewCollection;

    [ObservableProperty]
    private bool _acceptDuplicates;

    [ObservableProperty]
    private bool _acceptNewClips = true;

    [ObservableProperty]
    private CollectionType _collectionType = CollectionType.Normal;

    [ObservableProperty]
    private string _databaseInfo = string.Empty;

    [ObservableProperty]
    private string _icon = "📁";

    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private Guid? _parentId;

    [ObservableProperty]
    private int _purgingValue = 200;

    [ObservableProperty]
    private PurgingRule _selectedPurgingRule = PurgingRule.ByNumberOfItems;

    [ObservableProperty]
    private int _sortKey = 100;

    [ObservableProperty]
    private string _sqlQuery = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    public CollectionPropertiesViewModel(Collection collection, IConfigurationService configurationService, bool isNewCollection = false)
    {
        _collection = collection;
        _isNewCollection = isNewCollection;
        _configurationService = configurationService;

        LoadFromModel();
    }

    /// <summary>
    /// Whether purging rules are visible (Normal, Folder, Trashcan only).
    /// </summary>
    public bool ShowPurgingRules => CollectionType == CollectionType.Normal;

    /// <summary>
    /// Whether SQL editor is visible (Virtual collections only).
    /// </summary>
    public bool ShowSqlEditor => CollectionType == CollectionType.Virtual;

    /// <summary>
    /// Whether garbage avoidance options are visible (Normal collections only).
    /// </summary>
    public bool ShowGarbageAvoidance => CollectionType == CollectionType.Normal;

    /// <summary>
    /// Whether the purging value textbox should be enabled.
    /// </summary>
    public bool IsPurgingValueEnabled => SelectedPurgingRule != PurgingRule.Never;

    /// <summary>
    /// Label for purging value textbox (changes based on rule type).
    /// </summary>
    public string PurgingValueLabel => SelectedPurgingRule == PurgingRule.ByAge
        ? "Days:"
        : "Items:";

    /// <summary>
    /// Load properties from the Collection model.
    /// </summary>
    private void LoadFromModel()
    {
        Id = _collection.Id;
        ParentId = _collection.ParentId;
        Title = _collection.Title;
        Icon = _collection.Icon ?? "📁";
        IsFavorite = _collection.Favorite;
        SortKey = _collection.SortKey;
        AcceptNewClips = _collection.AcceptNewClips;
        AcceptDuplicates = _collection.AcceptDuplicates;
        IsReadOnly = _collection.ReadOnly;
        SqlQuery = _collection.Sql ?? string.Empty;

        // Determine collection type
        if (_collection.IsVirtual)
            CollectionType = CollectionType.Virtual;
        else if (_collection.IsFolder)
            CollectionType = CollectionType.Folder;
        else if (_collection.Title.Contains("Trash", StringComparison.OrdinalIgnoreCase))
            CollectionType = CollectionType.Trashcan;
        else
            CollectionType = CollectionType.Normal;

        // Determine purging rule
        if (_collection.RetentionLimit == 0)
        {
            SelectedPurgingRule = PurgingRule.Never;
            PurgingValue = 200; // Default value
        }
        else
        {
            // For now, we'll assume ByNumberOfItems
            // In ClipMate 7.5, there might be additional fields to distinguish between ByAge and ByNumberOfItems
            SelectedPurgingRule = PurgingRule.ByNumberOfItems;
            PurgingValue = _collection.RetentionLimit;
        }

        // TODO: Load item count from repository
        ItemCount = _collection.LastKnownCount ?? 0;

        // TODO: Load database info (might need to pass in database name)
        DatabaseInfo = "DBName: [My Clips], Version: [4.34 Build 2], User: [0]";
    }

    /// <summary>
    /// Save properties back to the Collection model.
    /// </summary>
    public void SaveToModel()
    {
        _collection.Title = Title;
        _collection.Icon = Icon;
        _collection.Favorite = IsFavorite;
        _collection.SortKey = SortKey;
        _collection.AcceptNewClips = AcceptNewClips;
        _collection.AcceptDuplicates = AcceptDuplicates;
        _collection.ReadOnly = IsReadOnly;

        // Set LmType based on collection type
        _collection.LmType = CollectionType switch
        {
            CollectionType.Virtual => CollectionLmType.Virtual,
            CollectionType.Folder => CollectionLmType.Folder,
            _ => CollectionLmType.Normal // Normal and Trashcan
        };

        // Set ListType for virtual collections
        if (CollectionType == CollectionType.Virtual)
        {
            _collection.ListType = CollectionListType.SqlBased;
            _collection.Sql = SqlQuery;
        }
        else
        {
            _collection.ListType = CollectionListType.Normal;
            _collection.Sql = null;
        }

        // Set retention limit based on purging rule
        _collection.RetentionLimit = SelectedPurgingRule == PurgingRule.Never
            ? 0
            : PurgingValue;

        _collection.LastUpdateTime = DateTime.UtcNow;
    }

    partial void OnCollectionTypeChanged(CollectionType value)
    {
        OnPropertyChanged(nameof(ShowPurgingRules));
        OnPropertyChanged(nameof(ShowSqlEditor));
        OnPropertyChanged(nameof(ShowGarbageAvoidance));
    }

    partial void OnSelectedPurgingRuleChanged(PurgingRule value)
    {
        OnPropertyChanged(nameof(IsPurgingValueEnabled));
        OnPropertyChanged(nameof(PurgingValueLabel));
    }

    [RelayCommand]
    private void ChangeIcon()
    {
        var picker = new EmojiPickerWindow(_configurationService, Icon);

        // Find the CollectionPropertiesWindow that owns this ViewModel
        var owner = Application.Current.Windows
            .OfType<CollectionPropertiesWindow>()
            .FirstOrDefault(w => w.DataContext == this);

        if (owner != null)
            picker.Owner = owner;

        if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedEmoji))
            Icon = picker.SelectedEmoji;
    }

    [RelayCommand]
    private void Ok()
    {
        SaveToModel();
        // Dialog is closed by ThemedWindow.DialogButtons with DialogResult=OK
    }

    [RelayCommand]
    private void Cancel()
    {
        // Dialog is closed by ThemedWindow.DialogButtons with DialogResult=Cancel
    }

    [RelayCommand]
    private void Help()
    {
        // TODO: Show context-sensitive help for Collection Properties
        MessageBox.Show(
            "Collection Properties Help:\n\n" +
            "• Normal: Standard collection for storing clips\n" +
            "• Folder: Organizational container for other collections\n" +
            "• Trashcan: Special collection for deleted items\n" +
            "• Virtual: Dynamic collection based on SQL query\n\n" +
            "Purging Rules control automatic clip deletion:\n" +
            "• By number: Keeps only the last N clips\n" +
            "• By age: Deletes clips older than N days\n" +
            "• Never: Safe collection with no auto-deletion",
            "Collection Properties Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
