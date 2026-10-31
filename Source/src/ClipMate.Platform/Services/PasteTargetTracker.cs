using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Helpers;
using ClipMate.Platform.Interop;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// <see cref="IPasteTargetTracker" /> with ClipMate 7's rules: a window on the GOOD list is suitable; one on the BAD
/// list, without a title or with a title on the bad-titles list is not; any other window is. ClipMate's own windows
/// never are.
/// </summary>
public sealed class PasteTargetTracker : IPasteTargetTracker, IDisposable
{
    // Bounds the window-stack walk; a desktop has a few hundred top-level windows at most
    private const int _maxWindowStackDepth = 1000;

    private readonly IConfigurationService _configurationService;
    private readonly IForegroundWindowMonitor _foregroundMonitor;
    private readonly ILogger<PasteTargetTracker> _logger;
    private readonly IMessenger _messenger;
    private readonly PasteTarget _pasteTarget;

    // Guards the target's read-modify-write: foreground changes arrive on the monitor's thread
    private readonly Lock _sync = new();
    private readonly IWin32InputInterop _win32;
    private TrackedWindow? _current;
    private bool _disposed;

    // The last suitable window seen while the target was locked, adopted when it is unlocked
    private TrackedWindow? _lastSuitableWhileLocked;
    private volatile TargetRules _rules = TargetRules.Empty;
    private bool _started;

    public PasteTargetTracker(IWin32InputInterop win32Interop,
        IForegroundWindowMonitor foregroundMonitor,
        PasteTarget pasteTarget,
        IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<PasteTargetTracker> logger)
    {
        _win32 = win32Interop ?? throw new ArgumentNullException(nameof(win32Interop));
        _foregroundMonitor = foregroundMonitor ?? throw new ArgumentNullException(nameof(foregroundMonitor));
        _pasteTarget = pasteTarget ?? throw new ArgumentNullException(nameof(pasteTarget));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        _foregroundMonitor.ForegroundChanged -= OnForegroundChanged;
    }

    /// <inheritdoc />
    public void Start()
    {
        lock (_sync)
        {
            if (_started || _disposed)
                return;

            _started = true;
        }

        _foregroundMonitor.ForegroundChanged += OnForegroundChanged;
        _foregroundMonitor.Start();

        // The window that is in front now is the starting point; later changes arrive through the monitor
        OnForegroundChanged(_win32.GetForegroundWindow());
        _logger.LogInformation("Paste Target tracking started");
    }

    /// <inheritdoc />
    public void SetLocked(bool locked)
    {
        var changed = false;
        lock (_sync)
        {
            _pasteTarget.SetLocked(locked);

            // Unlocking catches up with the windows the user switched to while the target was locked
            var adopt = _lastSuitableWhileLocked;
            _lastSuitableWhileLocked = null;
            if (!locked && adopt != null && _win32.IsWindow(adopt.Hwnd))
                changed = SetTarget(adopt);
        }

        _logger.LogDebug("Target lock set to {Locked}", locked);
        _messenger.Send(new StateRefreshRequestedEvent());
        if (changed)
            _messenger.Send(new QuickPasteTargetChangedEvent());
    }

    /// <summary>
    /// Follows the foreground window: a suitable window that is not ClipMate becomes the Paste Target (unless the
    /// target is locked); ClipMate coming to the front replaces a target that has closed or been hidden.
    /// </summary>
    private void OnForegroundChanged(HWND hwnd)
    {
        if (!_configurationService.Configuration.Preferences.QuickPasteAutoTargetingEnabled)
            return;

        var window = DescribeWindow(hwnd);
        if (window == null)
            return;

        bool changed;
        lock (_sync)
        {
            if (window.IsClipMate)
                changed = ReplaceUnavailableTarget(hwnd);
            else if (!IsSuitable(window))
                return;
            else if (_pasteTarget.IsLocked)
            {
                _lastSuitableWhileLocked = window;
                return;
            }
            else
                changed = SetTarget(window);
        }

        if (changed)
            _messenger.Send(new QuickPasteTargetChangedEvent());
    }

    /// <summary>
    /// When the Paste Target has closed or is hidden, takes the next suitable window below ClipMate's in the window
    /// stack instead. Called with <see cref="_sync" /> held.
    /// </summary>
    /// <returns>True when the target changed.</returns>
    private bool ReplaceUnavailableTarget(HWND clipMateWindow)
    {
        if (_pasteTarget.IsLocked)
            return false;

        var target = _current;
        if (target != null && IsShown(target.Hwnd))
            return false;

        var replacement = FindTargetInWindowStack(clipMateWindow);
        if (replacement != null)
        {
            _logger.LogInformation("Paste Target {Target} is no longer available; using {Replacement} from the window stack",
                target?.TargetString ?? "(none)", replacement.TargetString);

            return SetTarget(replacement);
        }

        // Nothing suitable is open: a closed target cannot be pasted into, but a hidden one may come back
        if (target == null || _win32.IsWindow(target.Hwnd))
            return false;

        _current = null;
        _pasteTarget.SetWindow(null);
        _logger.LogInformation("Paste Target {Target} has closed and no other window is suitable", target.TargetString);

        return true;
    }

    private TrackedWindow? FindTargetInWindowStack(HWND start)
    {
        var hwnd = start;
        for (var i = 0; i < _maxWindowStackDepth; i++)
        {
            hwnd = _win32.GetWindow(hwnd, GET_WINDOW_CMD.GW_HWNDNEXT);
            if (hwnd.IsNull)
                return null;

            if (!IsShown(hwnd))
                continue;

            var window = DescribeWindow(hwnd);
            if (window is { IsClipMate: false } && IsSuitable(window))
                return window;
        }

        return null;
    }

    private bool IsShown(HWND hwnd) =>
        !hwnd.IsNull && _win32.IsWindow(hwnd) && _win32.IsWindowVisible(hwnd) && !_win32.IsWindowCloaked(hwnd);

    /// <summary>
    /// Makes <paramref name="window" /> the Paste Target. Called with <see cref="_sync" /> held.
    /// </summary>
    /// <returns>True when the target changed.</returns>
    private bool SetTarget(TrackedWindow window)
    {
        if (window == _current)
            return false;

        _current = window;
        _pasteTarget.SetWindow(window.Target);
        _logger.LogInformation("Paste Target updated to: {Target} (Handle: {Handle:X})", window.TargetString, (nint)window.Hwnd);

        return true;
    }

    private TrackedWindow? DescribeWindow(HWND hwnd)
    {
        if (hwnd.IsNull)
            return null;

        try
        {
            _win32.GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0)
                return null;

            var processName = _win32.GetProcessName(processId);
            if (processName == null)
                return null;

            const int maxLength = 256;
            string className;
            string windowTitle;
            unsafe
            {
                var buffer = stackalloc char[maxLength];
                var length = _win32.GetClassName(hwnd, buffer, maxLength);
                className = length > 0
                    ? new string(buffer, 0, length).ToUpperInvariant()
                    : string.Empty;

                length = _win32.GetWindowText(hwnd, buffer, maxLength);
                windowTitle = length > 0
                    ? new string(buffer, 0, length)
                    : string.Empty;
            }

            return new TrackedWindow(hwnd, processId, new PasteTargetWindow(hwnd, processName.ToUpperInvariant(), className, windowTitle));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading window {Handle:X}", (nint)hwnd);

            return null;
        }
    }

    private bool IsSuitable(TrackedWindow window)
    {
        var rules = CurrentRules();
        var targetString = window.TargetString;

        if (rules.GoodTargets.Any(p => MatchesTarget(targetString, p)))
            return true;

        var badTarget = rules.BadTargets.FirstOrDefault(p => MatchesTarget(targetString, p));
        if (badTarget != null)
        {
            _logger.LogDebug("Window {Target} matches bad target {BadTarget}, rejecting", targetString, badTarget);

            return false;
        }

        var title = window.Target.WindowTitle;
        if (string.IsNullOrWhiteSpace(title))
        {
            _logger.LogDebug("Window {Target} has no title, rejecting", targetString);

            return false;
        }

        var badTitle = rules.BadTitles.FirstOrDefault(p => MatchesTitle(title, p));
        if (badTitle == null)
            return true;

        _logger.LogDebug("Window {Target} title {Title} matches bad title {BadTitle}, rejecting", targetString, title, badTitle);

        return false;
    }

    /// <summary>
    /// The GOOD, BAD and bad-titles lists as they stand. Settings replace a list rather than change it in place, so a
    /// copy taken whenever a list is replaced can be read here without racing the UI thread.
    /// </summary>
    private TargetRules CurrentRules()
    {
        var preferences = _configurationService.Configuration.Preferences;
        var rules = _rules;
        if (rules.IsFor(preferences))
            return rules;

        rules = TargetRules.From(preferences);
        _rules = rules;

        return rules;
    }

    private static bool MatchesTitle(string title, string pattern) =>
        !string.IsNullOrWhiteSpace(pattern) && title.StartsWith(pattern.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool MatchesTarget(string actual, string pattern)
    {
        // Pattern format: PROCESSNAME:CLASSNAME or PROCESSNAME: (match all classes for process)
        // Empty process name means match any process with that class
        var patternParts = pattern.Split(':', 2);
        var actualParts = actual.Split(':', 2);

        if (patternParts.Length != 2 || actualParts.Length != 2)
            return false;

        var patternProcess = patternParts[0];
        var patternClass = patternParts[1];
        var actualProcess = actualParts[0];
        var actualClass = actualParts[1];

        // Match process (empty pattern matches any, non-empty uses prefix match)
        if (!string.IsNullOrEmpty(patternProcess) &&
            !actualProcess.StartsWith(patternProcess, StringComparison.OrdinalIgnoreCase))
            return false;

        // Match class (empty pattern matches any, non-empty uses prefix match)
        return string.IsNullOrEmpty(patternClass) ||
               actualClass.StartsWith(patternClass, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A window as auto-targeting sees it. A record, so a changed title counts as a different window.
    /// </summary>
    private sealed record TrackedWindow(HWND Hwnd, uint ProcessId, PasteTargetWindow Target)
    {
        public bool IsClipMate => OwnProcess.Is(ProcessId);

        public string TargetString => Target.TargetString;
    }

    private sealed record TargetRules(
        List<string>? SourceGood,
        List<string>? SourceBad,
        List<string>? SourceBadTitles,
        string[] GoodTargets,
        string[] BadTargets,
        string[] BadTitles)
    {
        public static readonly TargetRules Empty = new(null, null, null, [], [], []);

        public static TargetRules From(PreferencesConfiguration preferences) => new(
            preferences.QuickPasteGoodTargets,
            preferences.QuickPasteBadTargets,
            preferences.QuickPasteBadTitles,
            [.. preferences.QuickPasteGoodTargets],
            [.. preferences.QuickPasteBadTargets],
            [.. preferences.QuickPasteBadTitles]);

        public bool IsFor(PreferencesConfiguration preferences) =>
            ReferenceEquals(SourceGood, preferences.QuickPasteGoodTargets) &&
            ReferenceEquals(SourceBad, preferences.QuickPasteBadTargets) &&
            ReferenceEquals(SourceBadTitles, preferences.QuickPasteBadTitles);
    }
}
