# Monaco Editor Refactoring - Complete

## Problem Analysis

The original implementation had several architectural issues:

1. **Three separate async calls** to WebView2 for loading content (SetTextAsync, SetLanguageAsync, RestoreViewStateAsync)
2. **Race conditions** when switching clips rapidly
3. **Complex initialization tracking** with polling and timeouts
4. **No proper debugging capabilities** to diagnose issues
5. **JavaScript logic scattered** between C# string building and HTML file

## Solution Implemented

### 1. Centralized JavaScript Functions (index.html)

Moved all Monaco operations into well-defined JavaScript functions:

- `initializeEditor(options)` - Single initialization point
- `loadContent(text, languageId, viewStateJson)` - **Atomic content loading**
- `saveViewState()` - View state persistence
- `updateOptions(options)` - Runtime option updates
- `setDebugMode(enabled)` - Toggle debugging

**Key Benefits:**
- One WebView2 round-trip instead of three
- No race conditions between setText/setLanguage/restoreViewState
- Proper error handling in JavaScript with messages back to C#

### 2. Proper Initialization Tracking

- Uses `TaskCompletionSource<bool>` to await initialization
- JavaScript sends `{ type: 'initialized', success: true/false }` message
- 5-second timeout with clear error message
- No more polling loops

### 3. Debug Mode

Added `EnableDebug` property to `MonacoEditorConfiguration`:

```csharp
public bool EnableDebug { get; set; } = false;
```

When enabled:
- Opens Chrome DevTools for the WebView2 instance
- Enables console logging in JavaScript
- Control name included in all log messages: `[TextEditor]` vs `[HtmlEditor]`

**To enable:** Set in configuration or via property:
```csharp
TextEditor.EnableDebug = true;
```

### 4. Comprehensive Logging

All operations now log with:
- Control name prefix `[TextEditor]` or `[HtmlEditor]`
- Operation context (IsInitialized state, content lengths, etc.)
- Clear error messages with context

Example log output:
```
[TextEditor] LoadContentAsync - Text: 1234 chars, Language: csharp, HasViewState: true, IsInitialized: true
[TextEditor] Executing loadContent JavaScript
[TextEditor] LoadContentAsync completed successfully
```

### 5. Atomic Content Loading

`LoadContentAsync()` now:
1. Validates initialization state
2. Escapes all content properly
3. Calls single JavaScript function: `loadContent(text, lang, viewState)`
4. Updates WPF properties with suppression flags
5. Returns success/failure

No more multi-step operations with timing dependencies.

## Files Changed

1. **Assets/monaco/index.html** - Complete rewrite with JavaScript functions
2. **MonacoEditorControl.xaml.cs**:
   - Added `EnableDebug` dependency property
   - Added `_initializationTcs` for proper async initialization
   - Refactored `OnNavigationCompleted` to use TaskCompletionSource
   - Refactored `LoadContentAsync` to use JavaScript function
   - Updated `SaveViewStateAsync` to use JavaScript function
   - Enhanced logging throughout with control names
3. **MonacoEditorConfiguration.cs** - Added `EnableDebug` property

## Usage

### Normal Operation
```csharp
await editor.LoadContentAsync(text, "csharp", viewStateJson);
```

### Debugging
```csharp
editor.EnableDebug = true; // Opens DevTools automatically
```

Or in configuration:
```json
{
  "MonacoEditor": {
    "EnableDebug": true,
    "Theme": "vs-dark",
    ...
  }
}
```

## Testing Recommendations

1. **Test with debug enabled** first to see JavaScript console
2. **Check logs** for `[TextEditor]` and `[HtmlEditor]` prefixes
3. **Switch clips rapidly** to verify no initialization timeouts
4. **Verify view state** persists (cursor position, scroll)

## Next Steps

If issues persist:
1. Enable debug mode
2. Check browser console in DevTools
3. Check application logs for `[Monaco]` entries
4. Look for initialization timeout errors (5 seconds)
