namespace ClipMate.Core.Services;

/// <summary>
/// Manages the Windows Firewall rule that lets Peers reach ClipMate while networking is enabled.
/// </summary>
public interface IFirewallRuleService
{
    /// <summary>
    /// Adds the inbound rule for ClipMate if it is missing. Adding a rule needs administrator rights,
    /// so Windows shows its standard elevation prompt. Returns <c>true</c> if the rule exists afterwards.
    /// </summary>
    Task<bool> EnsureInboundRuleAsync(CancellationToken cancellationToken = default);
}
