using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the pairing dialog. The same dialog is shown on both Devices: on the approving Device (a pairing
/// request arrived) and on the requesting Device (the user clicked <b>Pair</b>). Both show the Verification Code and a
/// "Show as" field, pre-filled with the other Device's Device Name, that sets the Peer Alias.
/// </summary>
public partial class PairingDialogViewModel : ObservableObject
{
    private readonly TaskCompletionSource<PairingDecision> _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string? _address;
    private readonly IPairingSession? _session;

    [ObservableProperty]
    private string _heading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    [NotifyPropertyChangedFor(nameof(DeclineText))]
    private bool _isFinished;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private bool _isWaiting;

    [ObservableProperty]
    private string _otherDeviceName;

    [ObservableProperty]
    private string _showAs;

    [ObservableProperty]
    private PairingStatus? _status;

    [ObservableProperty]
    private string _statusText;

    private PairingDialogViewModel(string heading, DeviceId otherDeviceId, string otherDeviceName, string showAs, string verificationCode,
        string statusText, IPairingSession? session)
    {
        _heading = heading;
        OtherDeviceId = otherDeviceId.ToDisplayString();
        _otherDeviceName = otherDeviceName;
        VerificationCode = verificationCode;
        _showAs = showAs;
        _statusText = statusText;
        _session = session;
        _address = session?.Target.Address;
        WhenPeerResponded = session == null ? Task.CompletedTask : ObservePeerResponseAsync(session);
    }

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Gets the other Device's full Device ID in grouped form.
    /// </summary>
    public string OtherDeviceId { get; }

    /// <summary>
    /// Gets the Verification Code; the other Device shows the same one.
    /// </summary>
    public string VerificationCode { get; }

    /// <summary>
    /// Gets the caption of the button that declines (or, once finished, closes the dialog).
    /// </summary>
    public string DeclineText => IsFinished ? "Close" : "Decline";

    /// <summary>
    /// Gets the user's decision on the approving Device. Completes when the user accepts or declines, or the request is withdrawn.
    /// </summary>
    public Task<PairingDecision> Decision => _decision.Task;

    /// <summary>
    /// Gets a task that completes once the other Device has answered (requesting Device only).
    /// </summary>
    public Task WhenPeerResponded { get; }

    /// <summary>
    /// Creates the dialog for an incoming pairing request.
    /// </summary>
    public static PairingDialogViewModel ForApproval(PairingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new PairingDialogViewModel($"{request.DeviceName} wants to pair with this Device", request.DeviceId, request.DeviceName,
            request.DeviceName, request.VerificationCode,
            "Accept only if the other Device shows the same Verification Code. Once paired, it can see the Databases you share.",
            null);
    }

    /// <summary>
    /// Creates the dialog for an outgoing pairing request that has been sent. A Device added manually has no Device Name
    /// yet: it is named by its address until it answers, and its Device Name then pre-fills "Show as".
    /// </summary>
    public static PairingDialogViewModel ForRequest(IPairingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var target = session.Target;
        var knownName = string.IsNullOrWhiteSpace(target.DeviceName) ? null : target.DeviceName;
        var name = knownName ?? $"the Device at {target.Address}";
        return new PairingDialogViewModel($"Pair with {name}", target.DeviceId, name, knownName ?? string.Empty, session.VerificationCode,
            $"Waiting for {name} to accept. Check that both Devices show the same Verification Code.", session);
    }

    /// <summary>
    /// The request was withdrawn by the requesting Device or expired (approving Device only): counts as declined.
    /// </summary>
    public void Abandon()
    {
        if (IsFinished)
            return;

        StatusText = "The pairing request was withdrawn or has expired.";
        Finish(PairingDecision.Declined, PairingStatus.Cancelled);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The dialog is closing (e.g. its close button): declines if nothing was decided yet.
    /// </summary>
    public void OnClosing()
    {
        if (!IsFinished)
            Decline();
    }

    private bool CanAccept() => !IsFinished && !IsWaiting;

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private async Task AcceptAsync()
    {
        var alias = NormalizeAlias(ShowAs);
        if (_session == null)
        {
            Finish(PairingDecision.Accept(alias), PairingStatus.Accepted);
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsWaiting = true;
        StatusText = $"Waiting for {OtherDeviceName} to accept...";
        var status = await _session.AcceptAsync(alias);
        IsWaiting = false;
        StatusText = Describe(status);
        Finish(PairingDecision.Accept(alias), status);
    }

    [RelayCommand]
    private void Decline()
    {
        if (!IsFinished)
        {
            _session?.Cancel();
            Finish(PairingDecision.Declined, PairingStatus.Cancelled);
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task ObservePeerResponseAsync(IPairingSession session)
    {
        var response = await session.PeerResponse;
        UseReportedDeviceName(session.PeerDeviceName);
        if (IsFinished || IsWaiting)
            return;

        if (response == PairingStatus.Accepted)
        {
            StatusText = $"{OtherDeviceName} accepted. Accept here too to finish pairing.";
            return;
        }

        StatusText = Describe(response);
        Finish(PairingDecision.Declined, response);
    }

    private void UseReportedDeviceName(string? reportedName)
    {
        if (string.IsNullOrWhiteSpace(reportedName) || reportedName == OtherDeviceName)
            return;

        var showAsUntouched = string.IsNullOrWhiteSpace(ShowAs) || ShowAs == OtherDeviceName;
        OtherDeviceName = reportedName;
        Heading = $"Pair with {reportedName}";
        if (showAsUntouched)
            ShowAs = reportedName;
    }

    private void Finish(PairingDecision decision, PairingStatus status)
    {
        Status = status;
        IsFinished = true;
        _decision.TrySetResult(decision);
    }

    private string? NormalizeAlias(string? showAs)
    {
        var alias = showAs?.Trim();
        return string.IsNullOrEmpty(alias) || alias == OtherDeviceName ? null : alias;
    }

    private string Describe(PairingStatus status) => status switch
    {
        PairingStatus.Paired => $"Paired with {OtherDeviceName}.",
        PairingStatus.Accepted => $"{OtherDeviceName} accepted.",
        PairingStatus.Declined => $"{OtherDeviceName} declined the pairing request.",
        PairingStatus.UpdateRequired => $"{OtherDeviceName} runs an incompatible version of ClipMate. Update ClipMate on both Devices, then try again.",
        PairingStatus.Busy => $"{OtherDeviceName} is busy with other pairing requests. Try again in a moment.",
        PairingStatus.Unreachable => $"Could not reach {OtherDeviceName}. Check that it is turned on, networking is enabled on it, and the address is right.",
        PairingStatus.WrongDevice =>
            $"The Device at {_address} does not have the Device ID you entered, so the connection was refused. Check the address and the Device ID.",
        PairingStatus.TimedOut => "The pairing request expired. Try again.",
        PairingStatus.Cancelled => "Pairing cancelled.",
        var _ => $"Pairing with {OtherDeviceName} failed.",
    };
}
