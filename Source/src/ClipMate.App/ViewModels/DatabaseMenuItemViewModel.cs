namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel representing a database item in a menu.
/// </summary>
/// <param name="DisplayName">The display name shown in the menu.</param>
/// <param name="DatabaseKey">The database key (file path) used to identify the database.</param>
public record DatabaseMenuItemViewModel(string DisplayName, string DatabaseKey);
