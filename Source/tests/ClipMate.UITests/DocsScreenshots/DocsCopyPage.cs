using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using ClipMate.UITests.Fixtures;

namespace ClipMate.UITests.DocsScreenshots;

/// <summary>
/// One isolated ClipMate as the docs script drives it. Windows are found by process ID with EnumWindows and searched
/// one at a time: searching the whole desktop through UI Automation can hang on another program's window. Every click
/// checks that the pixel belongs to this process. No clip row is ever clicked: selecting one writes to the clipboard.
/// </summary>
internal sealed class DocsCopyPage(Process app)
{
    public const string ExplorerTitle = "ClipMate - Clipboard Manager";
    public const string ClassicTitle = "ClipMate";
    public const string OptionsTitle = "Options";
    public const string PairingTitle = "Pairing";

    // The approving Device shows a notification first; the dialog opens by itself once that goes unanswered
    private static readonly TimeSpan _approvalDialogTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Waits for this copy's visible window titled <paramref name="title" />.
    /// </summary>
    public Task<string> WaitForWindowTitleAsync(string title, TimeSpan? timeout = null) =>
        Poll.UntilAsync(() => FindWindow(title) == 0 ? null : title, $"the \"{title}\" window", timeout);

    /// <summary>
    /// The visible window titled <paramref name="title" />. Throws if it isn't open.
    /// </summary>
    public nint Window(string title)
    {
        var window = FindWindow(title);
        return window != 0 ? window : throw new InvalidOperationException($"The \"{title}\" window is not open");
    }

    /// <summary>
    /// Waits until the window lists a clip row containing <paramref name="text" />. Only looks; selects nothing.
    /// </summary>
    public Task WaitForClipRowAsync(string title, string text) => Poll.UntilAsync(() =>
        FindWindow(title) is var window and not 0 && AutomationElement.FromHandle(window)
            .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem))
            .Cast<AutomationElement>()
            .Any(p => p.Current.Name.Contains(text, StringComparison.Ordinal)), $"a clip row containing \"{text}\"");

    /// <summary>
    /// Opens Tools &gt; Options... in the Explorer window with the real mouse. UI Automation's ExpandCollapse doesn't reveal
    /// the menu's items.
    /// </summary>
    public async Task OpenOptionsAsync()
    {
        await WaitForWindowTitleAsync(ExplorerTitle);
        var explorer = Window(ExplorerTitle);
        await Foreground.ActivateAsync(explorer, "the Explorer window");

        // A click made while the window is still settling can open no menu; click again until the item is there
        var attempt = 0;
        var options = await Poll.UntilAsync(() =>
        {
            if (FindInAnyWindow("Options...") is { } item)
                return item;

            if (attempt++ % 10 == 0)
                Foreground.TryLeftClickIn(app.Id, FindFirst(explorer, ControlType.MenuItem, "Tools").Current.BoundingRectangle);

            return null;
        }, "the Options... menu item");

        await Poll.UntilAsync(() => Foreground.TryLeftClickIn(app.Id, options.Current.BoundingRectangle), "the Options... menu item to be clickable");
        await WaitForWindowTitleAsync(OptionsTitle);
    }

    /// <summary>
    /// Selects the Options tab named <paramref name="tab" /> with the real mouse.
    /// </summary>
    public async Task SelectOptionsTabAsync(string tab)
    {
        var options = Window(OptionsTitle);
        await Foreground.ActivateAsync(options, "the Options dialog");

        // Looked up afresh each time: the tab control rebuilds its items while the dialog opens
        var attempt = 0;
        await Poll.UntilAsync(() =>
        {
            if (FindFirstOrDefault(options, ControlType.TabItem, tab) is not { } item)
                return false;

            if (item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection) && ((SelectionItemPattern)selection).Current.IsSelected)
                return true;

            if (attempt++ % 5 == 0)
                Foreground.TryLeftClickIn(app.Id, item.Current.BoundingRectangle);

            return false;
        }, $"the {tab} tab to be selected");
    }

    /// <summary>
    /// Closes the dialog titled <paramref name="title" /> with the button named <paramref name="button" />.
    /// </summary>
    public async Task CloseDialogAsync(string title, string button)
    {
        var dialog = Window(title);
        await Foreground.ActivateAsync(dialog, $"the \"{title}\" dialog");
        var found = FindFirst(dialog, ControlType.Button, button);
        await Poll.UntilAsync(() => Foreground.TryLeftClickIn(app.Id, found.Current.BoundingRectangle), $"the {button} button to be clickable");
        await Poll.UntilAsync(() => FindWindow(title) == 0, $"the \"{title}\" dialog to close");
    }

    /// <summary>
    /// Opens Options on its Devices tab, and waits until it lists the exact Device Name under Discovered Devices. Call this
    /// once the log reports the Device: the list is hidden while empty, and UI Automation doesn't see rows added after it
    /// first looked at the hidden list.
    /// </summary>
    public async Task OpenDevicesAsync(string discoveredDeviceName)
    {
        await OpenOptionsAsync();
        await SelectOptionsTabAsync("Devices");
        await Poll.UntilAsync(() => FindDiscoveredRow(discoveredDeviceName) != null, $"{discoveredDeviceName} under Discovered Devices");
    }

    /// <summary>
    /// Clicks Pair... on the row whose Device Name is exactly <paramref name="deviceName" />, never on another Device's row.
    /// </summary>
    public async Task PairWithAsync(string deviceName)
    {
        var options = Window(OptionsTitle);
        await Foreground.ActivateAsync(options, "the Options dialog");
        var row = FindDiscoveredRow(deviceName) ?? throw new InvalidOperationException($"{deviceName} is not listed");
        var button = row.FindFirst(TreeScope.Descendants, new AndCondition(
                         new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                         new PropertyCondition(AutomationElement.NameProperty, "Pair...")))
                     ?? throw new InvalidOperationException($"{deviceName}'s row has no Pair... button");

        await Poll.UntilAsync(() => Foreground.TryLeftClickIn(app.Id, button.Current.BoundingRectangle), "the Pair... button to be clickable");
        await WaitForWindowTitleAsync(PairingTitle);
    }

    /// <summary>
    /// Waits for the notification of an incoming pairing request, the window showing <paramref name="message" />.
    /// </summary>
    public async Task<nint> WaitForPairingNotificationAsync(string message)
    {
        nint window = 0;
        await Poll.UntilAsync(() =>
        {
            window = VisibleWindows().FirstOrDefault(p => AutomationElement.FromHandle(p).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, message)) != null);

            return window != 0;
        }, "the pairing request notification", _approvalDialogTimeout);

        return window;
    }

    /// <summary>
    /// Waits for the Pairing dialog of an incoming request.
    /// </summary>
    public Task WaitForApprovalDialogAsync() => WaitForWindowTitleAsync(PairingTitle, _approvalDialogTimeout);

    /// <summary>
    /// Clicks Accept in the open Pairing dialog.
    /// </summary>
    public async Task AcceptPairingAsync()
    {
        var dialog = Window(PairingTitle);
        await Foreground.ActivateAsync(dialog, "the Pairing dialog");
        var accept = FindFirst(dialog, ControlType.Button, "Accept");

        // The two copies' dialogs lie on top of each other, and a click made as the other one closes can be lost. Accept is
        // disabled, or the dialog closed, once a click has taken effect; a click on this copy's disabled button does nothing.
        var attempt = 0;
        await Poll.UntilAsync(() =>
        {
            if (FindWindow(PairingTitle) == 0 || !accept.Current.IsEnabled)
                return true;

            if (attempt++ % 5 == 0)
                Foreground.TryLeftClickIn(app.Id, accept.Current.BoundingRectangle);

            return false;
        }, "the Accept button to take effect");
    }

    /// <summary>
    /// Opens Options afresh and waits until its Peers list shows <paramref name="deviceName" /> (a row starting with
    /// Name; Device Name). Afresh for the same reason as <see cref="OpenDevicesAsync" />: the Peers list was hidden before.
    /// </summary>
    public async Task WaitForPeerAsync(string deviceName)
    {
        await CloseDialogAsync(OptionsTitle, "Cancel");
        await OpenOptionsAsync();
        await SelectOptionsTabAsync("Devices");
        await Poll.UntilAsync(() => AutomationElement.FromHandle(Window(OptionsTitle))
            .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem))
            .Cast<AutomationElement>()
            .Any(p => p.Current.Name.StartsWith($"{deviceName}; {deviceName}; ", StringComparison.Ordinal)), $"{deviceName} under Peers");
    }

    /// <summary>
    /// A Discovered Devices row is named "Device Name; Device ID; Status; ". A Peers row has more fields and no Pair... button.
    /// </summary>
    private AutomationElement? FindDiscoveredRow(string deviceName) => AutomationElement.FromHandle(Window(OptionsTitle))
        .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem))
        .Cast<AutomationElement>()
        .FirstOrDefault(p =>
        {
            var fields = p.Current.Name.Split("; ");
            return fields.Length == 4 && fields[0] == deviceName;
        });

    private AutomationElement? FindInAnyWindow(string name)
    {
        foreach (var item in VisibleWindows())
        {
            if (AutomationElement.FromHandle(item).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name)) is { } found)
                return found;
        }

        return null;
    }

    private static AutomationElement FindFirst(nint window, ControlType type, string name) =>
        FindFirstOrDefault(window, type, name) ?? throw new InvalidOperationException($"No {type.ProgrammaticName} named \"{name}\"");

    private static AutomationElement? FindFirstOrDefault(nint window, ControlType type, string name) =>
        AutomationElement.FromHandle(window).FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, type),
            new PropertyCondition(AutomationElement.NameProperty, name)));

    private nint FindWindow(string title) => VisibleWindows().FirstOrDefault(p => TitleOf(p) == title);

    private List<nint> VisibleWindows()
    {
        var windows = new List<nint>();
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var owner);
            if (owner == app.Id && IsWindowVisible(window))
                windows.Add(window);

            return true;
        }, 0);

        return windows;
    }

    private static string TitleOf(nint window)
    {
        var title = new StringBuilder(256);
        GetWindowText(window, title, title.Capacity);
        return title.ToString();
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int maxCount);
}
