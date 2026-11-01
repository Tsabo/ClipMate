namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// When the Outbound Filter puts a copy made in another program back on the clipboard filtered.
/// </summary>
public enum OutboundFilterMode
{
    /// <summary>
    /// Right after the copy is captured.
    /// </summary>
    Active,

    /// <summary>
    /// When ClipMate loses focus, as ClipMate 6.5 did.
    /// </summary>
    Passive,
}
