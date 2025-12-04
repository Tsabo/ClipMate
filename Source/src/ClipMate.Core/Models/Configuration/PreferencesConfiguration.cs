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
    /// Gets or sets whether to hide the taskbar icon.
    /// </summary>
    public bool HideTaskbarIcon { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show system tray icon.
    /// </summary>
    public bool ShowSystemTrayIcon { get; set; } = true;

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
    /// Gets or sets the hint hide pause duration (in milliseconds).
    /// </summary>
    public int HintHidePause { get; set; } = 4500;

    /// <summary>
    /// Gets or sets the language/locale.
    /// </summary>
    public string Language { get; set; } = "English";

    /// <summary>
    /// Gets or sets the logging level (0=None, 1=Error, 2=Warning, 3=Info, 4=Debug, 5=Verbose).
    /// </summary>
    public int LogLevel { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether the application has been registered (licensed).
    /// </summary>
    public bool IsRegistered { get; set; } = false;

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
    public string PowerPasteDelimiter { get; set; } = ",.;:\\n\\t";

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
}
