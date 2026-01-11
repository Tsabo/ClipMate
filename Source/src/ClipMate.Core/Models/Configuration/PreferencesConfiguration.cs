using Microsoft.Extensions.Logging;

namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// General application preferences.
/// </summary>
public class PreferencesConfiguration
{
    /// <summary>
    /// Gets or sets whether auto-capture is enabled at startup.
    /// </summary>
    public bool AutoCaptureAtStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show the system tray icon.
    /// </summary>
    public bool ShowTrayIcon { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show the application in the Windows taskbar.
    /// </summary>
    public bool ShowTaskbarIcon { get; set; } = true;

    /// <summary>
    /// Gets or sets the action to perform when left-clicking the system tray icon.
    /// </summary>
    public IconLeftClickAction TrayIconLeftClickAction { get; set; } = IconLeftClickAction.ShowExplorerWindow;

    /// <summary>
    /// Gets or sets the action to perform when left-clicking the taskbar icon.
    /// Note: WPF has limited support for taskbar icon click detection.
    /// </summary>
    public IconLeftClickAction TaskbarIconLeftClickAction { get; set; } = IconLeftClickAction.ShowExplorerWindow;

    /// <summary>
    /// Gets or sets where the ClipBar popup window should appear.
    /// </summary>
    public ClipBarPopupLocation ClipBarPopupLocation { get; set; } = ClipBarPopupLocation.RememberLastLocation;

    /// <summary>
    /// Gets or sets the last remembered position of the ClipBar window (format: "X,Y").
    /// Used when ClipBarPopupLocation is set to RememberLastLocation.
    /// </summary>
    public string? ClipBarLastPosition { get; set; }

    /// <summary>
    /// Gets or sets whether ClipMate Classic window stays on top of other windows.
    /// </summary>
    public bool ClassicStayOnTop { get; set; } = true;

    /// <summary>
    /// Gets or sets whether ClipMate Classic window is in dropped-down state.
    /// </summary>
    public bool ClassicIsDroppedDown { get; set; } = false;

    /// <summary>
    /// Gets or sets whether ClipMate Classic window is tacked (pinned) in dropped-down state.
    /// </summary>
    public bool ClassicIsTacked { get; set; } = false;

    /// <summary>
    /// Gets or sets the saved window state for Classic window (when invoked from hotkey).
    /// </summary>
    public WindowStateConfiguration ClassicWindowHotkey { get; set; } = new();

    /// <summary>
    /// Gets or sets the saved window state for Classic window (when invoked from taskbar).
    /// </summary>
    public WindowStateConfiguration ClassicWindowTaskbar { get; set; } = new();

    /// <summary>
    /// Gets or sets the saved expanded height for Classic window when dropdown is shown.
    /// </summary>
    public double ClassicExpandedHeight { get; set; } = 250;

    /// <summary>
    /// Gets or sets the saved window state for Explorer window.
    /// </summary>
    public WindowStateConfiguration ExplorerWindow { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to capture existing clipboard on startup.
    /// </summary>
    public bool CaptureExistingClipboard { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay (in milliseconds) after copy operation.
    /// </summary>
    public int DelayAfterCopy { get; set; } = 999;

    /// <summary>
    /// Gets or sets the delay (in milliseconds) on clipboard update.
    /// </summary>
    public int DelayOnClipboardUpdate { get; set; } = 250;

    /// <summary>
    /// Gets or sets whether to show hints/tooltips.
    /// </summary>
    public bool ShowHint { get; set; } = true;

    /// <summary>
    /// Gets or sets the backup interval in days for all databases.
    /// 0 or 9999 means never backup automatically.
    /// </summary>
    public int BackupIntervalDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the number of seconds to auto-confirm the backup dialog.
    /// 0 means auto-confirm is disabled.
    /// </summary>
    public int AutoConfirmBackupSeconds { get; set; } = 0;

    /// <summary>
    /// Gets or sets the hint hide pause duration (in milliseconds).
    /// </summary>
    public int HintHidePause { get; set; } = 4500;

    /// <summary>
    /// Gets or sets the language/locale.
    /// </summary>
    public string Language { get; set; } = "English";

    /// <summary>
    /// Gets or sets the logging level.
    /// Valid values: Trace, Debug, Information, Warning, Error, Critical, None.
    /// Default is Information.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether the application has been registered (licensed).
    /// </summary>
    public bool IsRegistered { get; set; } = false;

    // ==================== Advanced Tab Settings ====================

    /// <summary>
    /// Gets or sets the startup delay in seconds.
    /// Causes ClipMate to pause before initializing to prevent conflicts with other applications.
    /// </summary>
    public int StartupDelaySeconds { get; set; } = 0;

    /// <summary>
    /// Gets or sets the capture delay in milliseconds.
    /// Causes ClipMate to pause after copy operations to let the copying application finish.
    /// </summary>
    public int CaptureDelayMs { get; set; } = 250;

    /// <summary>
    /// Gets or sets the settle time between captures in milliseconds.
    /// Minimum time to wait after capturing before accepting new items.
    /// </summary>
    public int SettleTimeBetweenCapturesMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether the ALT key is required for collection tree drag/drop.
    /// Prevents unintended re-arrangement of collections.
    /// </summary>
    public bool AltKeyRequiredForDragDrop { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to pay attention to the clipboard viewer ignore flag.
    /// When enabled, ClipMate ignores updates from programs that request to be ignored.
    /// </summary>
    public bool PayAttentionToClipboardIgnoreFlag { get; set; } = true;

    /// <summary>
    /// Gets or sets whether cached database writes are enabled.
    /// For performance, database updates are cached. Disable only if experiencing data loss from crashes.
    /// </summary>
    public bool EnableCachedDatabaseWrites { get; set; } = true;

    /// <summary>
    /// Gets or sets whether Move/Copy buttons should re-use the last selected target collection.
    /// </summary>
    public bool ReuseLastSelectedMoveTarget { get; set; } = false;

    /// <summary>
    /// Gets or sets the PowerPaste delay (in milliseconds).
    /// </summary>
    public int PowerPasteDelay { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether PowerPaste shield is enabled (prevents accidental activation).
    /// </summary>
    public bool PowerPasteShield { get; set; } = true;

    /// <summary>
    /// Gets or sets the PowerPaste delimiter characters.
    /// </summary>
    public string PowerPasteDelimiter { get; set; } = @",.;:\n\t";

    /// <summary>
    /// Gets or sets whether to trim items during PowerPaste.
    /// </summary>
    public bool PowerPasteTrim { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include delimiter in PowerPaste.
    /// </summary>
    public bool PowerPasteIncludeDelimiter { get; set; } = false;

    /// <summary>
    /// Gets or sets whether PowerPaste should loop.
    /// </summary>
    public bool PowerPasteLoop { get; set; } = false;

    /// <summary>
    /// Gets or sets whether PowerPaste should explode (split) items.
    /// </summary>
    public bool PowerPasteExplode { get; set; } = false;

    /// <summary>
    /// Gets or sets the last PowerPaste direction used (for persistence across sessions).
    /// Valid values: "Up", "Down", or null/empty for default.
    /// </summary>
    public string? PowerPasteLastDirection { get; set; }

    /// <summary>
    /// Gets or sets how timestamps should be displayed in the UI.
    /// True = show in local timezone, False = show in original captured timezone with offset.
    /// </summary>
    public bool ShowTimestampsInLocalTime { get; set; } = true;

    // ==================== Sound Configuration ====================

    /// <summary>
    /// Gets or sets the sound configuration for various clipboard and paste events.
    /// </summary>
    public SoundConfiguration Sound { get; set; } = new();

    // ==================== General Tab Settings ====================

    /// <summary>
    /// Gets or sets whether to load the Classic view at startup.
    /// </summary>
    public bool LoadClassicAtStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to load the Explorer view at startup.
    /// </summary>
    public bool LoadExplorerAtStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets which view to show initially when ClipMate starts.
    /// </summary>
    public InitialShowMode InitialShowMode { get; set; } = InitialShowMode.Explorer;

    /// <summary>
    /// Gets or sets whether ClipMate should start automatically when Windows starts.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to confirm when deleting items from "safe" collections.
    /// </summary>
    public bool ConfirmDeletionFromSafeCollections { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically check for updates.
    /// </summary>
    public bool CheckUpdatesAutomatically { get; set; } = false;

    /// <summary>
    /// Gets or sets the interval (in days) between automatic update checks.
    /// </summary>
    public int UpdateCheckIntervalDays { get; set; } = 5;

    /// <summary>
    /// Gets or sets the last date/time when an update check was performed.
    /// Used internally to track update check intervals.
    /// </summary>
    public DateTime? LastUpdateCheckDate { get; set; }

    /// <summary>
    /// Gets or sets whether to sort collections alphabetically.
    /// </summary>
    public bool SortCollectionsAlphabetically { get; set; } = false;

    /// <summary>
    /// Gets or sets whether mouse wheel scrolling selects a clip.
    /// </summary>
    public bool MousewheelSelectsClip { get; set; } = false;

    /// <summary>
    /// Gets or sets what happens when clicking a collection icon.
    /// </summary>
    public CollectionIconClickBehavior CollectionIconClickBehavior { get; set; } = CollectionIconClickBehavior.MenuAppears;

    /// <summary>
    /// Gets or sets the layout mode for the Explorer view.
    /// </summary>
    public ExplorerLayoutMode ExplorerLayout { get; set; } = ExplorerLayoutMode.FullWidthEditor;

    // ==================== Capturing Tab Settings ====================

    /// <summary>
    /// Gets or sets whether auto-capture is enabled when the application starts.
    /// When false, clipboard monitoring must be manually enabled.
    /// </summary>
    public bool EnableAutoCaptureAtStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to capture the existing clipboard content when starting auto-capture.
    /// When true, the current clipboard content is captured as the first clip.
    /// </summary>
    public bool CaptureExistingClipboardAtStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically expand HDROP file pointers to text when capturing.
    /// This is a UI-only setting as HDROP expansion is always performed internally.
    /// </summary>
    public bool AutoExpandHdropFilePointers { get; set; } = true;

    /// <summary>
    /// Gets or sets the default value for AcceptClipsFromClipboard when creating new collections.
    /// When true, newly created collections will automatically receive clipboard captures.
    /// </summary>
    public bool DefaultAcceptClipsFromClipboard { get; set; } = true;

    /// <summary>
    /// Gets or sets the separator string to use when appending clips together.
    /// Supports escape sequences: \n (newline), \t (tab), \r (carriage return).
    /// Empty string is allowed for direct concatenation.
    /// </summary>
    public string AppendSeparatorString { get; set; } = "\r\n";

    /// <summary>
    /// Gets or sets whether to strip trailing line breaks when appending clips.
    /// When true, removes \r\n, \n, or \r from the end of each clip before appending.
    /// </summary>
    public bool StripTrailingLineBreak { get; set; } = false;

    // ==================== Editor Tab Settings ====================

    /// <summary>
    /// Gets or sets whether binary view is enabled in the clip viewer.
    /// </summary>
    public bool EnableBinaryView { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically change clip titles to reflect manual edits.
    /// </summary>
    public bool AutoChangeClipTitles { get; set; } = false;

    /// <summary>
    /// Gets or sets the default editor view type to show when displaying clips.
    /// </summary>
    public EditorViewType DefaultEditorView { get; set; } = EditorViewType.Text;

    // ==================== QuickPaste Tab Settings ====================

    /// <summary>
    /// Gets or sets whether auto-targeting is enabled for QuickPaste.
    /// When enabled, ClipMate automatically detects the target application for pasting.
    /// </summary>
    public bool QuickPasteAutoTargetingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use a monitoring thread for auto-targeting.
    /// Useful when ClipMate is running in "always on top" mode.
    /// </summary>
    public bool QuickPasteUseMonitoringThread { get; set; } = false;

    /// <summary>
    /// Gets or sets whether pressing ENTER on a clip triggers QuickPaste.
    /// </summary>
    public bool QuickPastePasteOnEnter { get; set; } = true;

    /// <summary>
    /// Gets or sets whether double-clicking a clip triggers QuickPaste.
    /// </summary>
    public bool QuickPastePasteOnDoubleClick { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of good program/class combinations for auto-targeting.
    /// Format: PROCESSNAME:CLASSNAME (e.g., "FIREFOX:MOZILLAUIWINDOWCLASS")
    /// </summary>
    public List<string> QuickPasteGoodTargets { get; set; } =
    [
        "FIREFOX:MOZILLAUIWINDOWCLASS",
        "IEXPLORE:IEFRAME",
        "EXPLORER:CABINETWCLASS",
        "WINWORD:OPUSAPP",
        ":MSWINPUB",
        "DEVENV:WNDCLASS_DESKED_GSK",
        "OUTLOOK:RCTRL_NOTEWND32",
        ":PP12FRAMECLASS",
        ":FRONTPAGEEXPLORERWINDOW40",
        ":XLMAIN",
        ":TFRMHELPMAN",
        "EXCEL:XLMAIN",
        "MSPUB:MSWINPUB",
        "POWERPNT:PP12FRAMECLASS",
    ];

    /// <summary>
    /// Gets or sets the list of bad program/class combinations to exclude from auto-targeting.
    /// Format: PROCESSNAME:CLASSNAME or PROCESSNAME: (exclude entire application)
    /// </summary>
    public List<string> QuickPasteBadTargets { get; set; } =
    [
        "CLIPMATE:",
        "POWERPNT:PROPERTIES",
        "WINWORD:MSOCOMMANDBARPOPUP",
        "WINWORD:MSOCOMMANDBARSHADOW",
        ":MSCTFIME_UI",
        ":OFFICETOOLTIP",
        "IEXPLORE:AUTO-SUGGEST_DROPDOWN",
        ":MSOCOMMANDBARSHADOW",
        ":MSOCOMMANDBARPOPUP",
        ":#43",
        "HELPMAN:TFRMTOPIC",
        ":GDI+_HOOK_WINDOW_CLASS",
    ];

    /// <summary>
    /// Gets or sets the list of QuickPaste formatting strings.
    /// These define how keystrokes are sent to target applications during paste operations.
    /// </summary>
    public List<QuickPasteFormattingString> QuickPasteFormattingStrings { get; set; } =
    [
        new()
            { Title = "Paste Ctrl+V", Preamble = "", PasteKeystrokes = "^v", Postamble = "", TitleTrigger = "*" },
        new()
            { Title = "Paste Shift+Ins", Preamble = "", PasteKeystrokes = "~{INSERT}", Postamble = "", TitleTrigger = "" },
        new()
            { Title = "Paste Edit Menu", Preamble = "", PasteKeystrokes = "@e#PAUSE#p", Postamble = "", TitleTrigger = "" },
        new()
            { Title = "Paste + ENTER", Preamble = "", PasteKeystrokes = "^v", Postamble = "{ENTER}", TitleTrigger = "" },
        new()
            { Title = "Paste + TAB", Preamble = "", PasteKeystrokes = "^v", Postamble = "{TAB}", TitleTrigger = "" },
        new()
            { Title = "Paste + Time", Preamble = "", PasteKeystrokes = "^v", Postamble = "{ENTER}Captured At: #DATE# #TIME#", TitleTrigger = "" },
        new()
            { Title = "Current Date_Time", Preamble = "The Date_Time Is:", PasteKeystrokes = "", Postamble = "#CURRENTDATE# #CURRENTTIME#", TitleTrigger = "" },
        new()
            { Title = "Clip + URL", Preamble = "", PasteKeystrokes = "^v", Postamble = "{ENTER}#URL#", TitleTrigger = "" },
        new()
            { Title = "Title, Clip, URL", Preamble = "#TITLE#:{ENTER}", PasteKeystrokes = "@e p", Postamble = "{ENTER}URL:#URL#{ENTER}", TitleTrigger = "" },
        new()
            { Title = "Sequence + Paste", Preamble = "Item Nbr: #SEQUENCE#{TAB}", PasteKeystrokes = "^v", Postamble = "", TitleTrigger = "" },
    ];

    /// <summary>
    /// Gets or sets whether to use pictures from browser cache when displaying HTML.
    /// </summary>
    public bool HtmlUsePicturesFromCache { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to use all HTML content in clip (including headers).
    /// </summary>
    public bool HtmlUseAllHtmlInClip { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to delete/strip scripts from HTML content for security.
    /// </summary>
    public bool HtmlDeleteScripts { get; set; } = true;
}
