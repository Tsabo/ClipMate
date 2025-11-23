# Collection Properties Implementation

## Overview

This document summarizes the implementation of the Collection Properties dialog for ClipMate, matching the ClipMate 7.5 functionality shown in your screenshots.

## Components Created

### 1. Model Enums

**`CollectionType.cs`** - Enum defining the four collection types:
- `Normal` (0) - Standard collection
- `Folder` (2) - Hierarchical folder
- `Trashcan` (3) - Special trash collection  
- `Virtual` (1) - SQL-based dynamic collection

**`PurgingRule.cs`** - Enum defining auto-deletion rules:
- `ByNumberOfItems` - Keep last N clips
- `ByAge` - Delete clips older than N days
- `Never` - Safe collection (no auto-deletion)

### 2. ViewModel

**`CollectionPropertiesViewModel.cs`** - MVVM ViewModel with:

**Properties:**
- Basic Info: `Title`, `Id`, `ParentId`, `Icon`, `IsFavorite`, `SortKey`
- Collection Type: `CollectionType` (Normal/Folder/Trashcan/Virtual)
- Purging: `SelectedPurgingRule`, `PurgingValue`
- Garbage Avoidance: `AcceptNewClips`, `AcceptDuplicates`, `IsReadOnly`
- Virtual Collection: `SqlQuery`
- Display: `ItemCount`, `DatabaseInfo`

**Computed Properties:**
- `ShowPurgingRules` - Visible for Normal/Folder/Trashcan
- `ShowSqlEditor` - Visible for Virtual collections only
- `ShowGarbageAvoidance` - Visible for Normal collections only
- `IsPurgingValueEnabled` - Disabled when "Never" selected
- `PurgingValueLabel` - Changes between "Items:" and "Days:"

**Commands:**
- `ChangeIconCommand` - Opens icon picker (TODO)
- `OkCommand` - Saves and closes dialog
- `CancelCommand` - Closes without saving
- `HelpCommand` - Shows context help

**Methods:**
- `LoadFromModel()` - Populates ViewModel from `Collection` entity
- `SaveToModel()` - Persists changes back to `Collection` entity

### 3. View

**`CollectionPropertiesWindow.xaml`** - DevExpress ThemedWindow with:

**Layout using DevExpress LayoutControl:**
- Header: Item count and database info (read-only)
- ID Section: GUID display for ID and ParentID (TextEdit read-only)
- Title: Editable text field (TextEdit)
- Icon/Favorite/SortKey: ButtonEdit for icon picker, CheckEdit for favorite, SpinEdit for sort key
- Collection Type: Radio buttons in LayoutGroup (Normal/Folder/Trashcan/Virtual)
- Purging Rules: Conditional LayoutGroup with 3 radio options + SpinEdit value input
- Garbage Avoidance: Conditional LayoutGroup with CheckEdit controls
- SQL Editor: Conditional MemoEdit for virtual collection queries
- Buttons: ThemedWindowDialogButton (OK, Cancel, Help)

**Dynamic Behavior:**
- Content changes based on `CollectionType` selection
- Purging value SpinEdit enables/disables based on rule selection
- Uses DevExpress ThemedWindow styling
- Emoji support via emoji.wpf library
- No ScrollViewer needed - all controls fit properly in dialog

### 4. Emoji Picker

**`EmojiPickerWindow.xaml`** - DevExpress ThemedWindow with:
- `emoji:Picker` control for selecting emojis
- OK/Cancel dialog buttons
- Returns selected emoji string

**`EmojiPickerWindow.xaml.cs`**:
- `SelectedEmoji` property to retrieve picked emoji
- Handles `Picked` event from Emoji.Wpf.Picker

### 5. Converter

**`EnumToBooleanConverter.cs`** - IValueConverter for binding radio buttons to enum properties

### 6. App.xaml Updates

Registered global converters:
- `EnumToBooleanConverter`
- `BooleanToVisibilityConverter`

## Usage

```csharp
// Create and show dialog
var collection = await _collectionRepository.GetByIdAsync(collectionId);
var viewModel = new CollectionPropertiesViewModel(collection);
var window = new CollectionPropertiesWindow(viewModel);

if (window.ShowDialog() == true)
{
    // Save changes to database
    await _collectionRepository.UpdateAsync(collection);
}
```

## SQL Query Security Consideration

As you mentioned, we need to handle SQL queries for virtual collections carefully. Current considerations:

### Option 1: Read-Only Query Enforcement
- Parse SQL and reject INSERT/UPDATE/DELETE statements
- Allow only SELECT queries
- Whitelist specific tables (Clip, ClipData)

### Option 2: Stored Procedures/Views
- Replace free-form SQL with predefined views or procedures
- User selects from dropdown of pre-built queries
- Much safer but less flexible

### Option 3: Query Builder UI
- Visual query builder instead of raw SQL
- Generates safe SELECT queries under the hood
- Best UX but most complex to implement

### Recommendation
Start with Option 1 (read-only enforcement) for MVP:
- Validate SQL before executing
- Use regex or SQL parser to detect DML statements
- Show warning message if dangerous SQL detected
- Consider using SQLite's `PRAGMA query_only` mode

## TODO Items

1. ✅ **Icon Picker**: Implemented `ChangeIconCommand` with Emoji.Wpf.Picker dialog
2. ✅ **Dialog Result**: ThemedWindow.DialogButtons handle OK/Cancel/Help automatically
3. ✅ **Help System**: Basic help MessageBox implemented for HelpCommand
4. **SQL Validation**: Add SQL query validator for virtual collections
5. **Item Count**: Connect to repository to get actual clip count
6. **Database Info**: Pass database metadata (name, version, user) to ViewModel
7. **Age-Based Purging**: Implement additional field if ClipMate 7.5 distinguishes between count/age purging
8. **Integration**: Wire up from CollectionTreeView context menu to open properties dialog

## Files Created/Modified

**New Files:**
- `Source/src/ClipMate.Core/Models/CollectionType.cs`
- `Source/src/ClipMate.Core/Models/PurgingRule.cs`
- `Source/src/ClipMate.App/ViewModels/CollectionPropertiesViewModel.cs`
- `Source/src/ClipMate.App/Views/CollectionPropertiesWindow.xaml`
- `Source/src/ClipMate.App/Views/CollectionPropertiesWindow.xaml.cs`
- `Source/src/ClipMate.App/Views/EmojiPickerWindow.xaml`
- `Source/src/ClipMate.App/Views/EmojiPickerWindow.xaml.cs`
- `Source/src/ClipMate.App/Converters/EnumToBooleanConverter.cs`

**Modified Files:**
- `Source/src/ClipMate.App/App.xaml`

## Next Steps

1. Test the dialog with different collection types
2. Wire up to collection tree context menu
3. Implement SQL validation for virtual collections
4. Add icon picker functionality
5. Connect to database for real item counts
