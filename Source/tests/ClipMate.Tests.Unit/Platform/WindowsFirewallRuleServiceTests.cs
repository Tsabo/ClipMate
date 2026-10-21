using System.ComponentModel;
using ClipMate.Platform.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Platform;

public class WindowsFirewallRuleServiceTests
{
    private const string _programPath = @"C:\Program Files\ClipMate\ClipMate.App.exe";
    private INetshRunner _runner = null!;
    private WindowsFirewallRuleService _service = null!;

    [Before(Test)]
    public void Setup()
    {
        _runner = A.Fake<INetshRunner>();
        _service = new WindowsFirewallRuleService(_runner, _programPath, NullLogger<WindowsFirewallRuleService>.Instance);
    }

    [Test]
    public async Task EnsureInboundRuleAsync_RuleAlreadyAllowsThisProgram_DoesNotElevate()
    {
        // Arrange
        A.CallTo(() => _runner.RunAsync(A<string>.That.StartsWith("advfirewall firewall show rule"), false, A<CancellationToken>._))
            .Returns(new NetshResult(0, $"Rule Name: ClipMate Peer Networking\r\nProgram: {_programPath.ToUpperInvariant()}\r\n"));

        // Act
        var result = await _service.EnsureInboundRuleAsync();

        // Assert
        await Assert.That(result).IsTrue();
        A.CallTo(() => _runner.RunAsync(A<string>._, true, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task EnsureInboundRuleAsync_RuleMissing_AddsInboundAllowRuleForThisProgramElevated()
    {
        // Arrange
        A.CallTo(() => _runner.RunAsync(A<string>._, false, A<CancellationToken>._)).Returns(new NetshResult(1, "No rules match the specified criteria."));
        A.CallTo(() => _runner.RunAsync(A<string>._, true, A<CancellationToken>._)).Returns(new NetshResult(0, string.Empty));

        // Act
        var result = await _service.EnsureInboundRuleAsync();

        // Assert
        await Assert.That(result).IsTrue();
        A.CallTo(() => _runner.RunAsync(
                A<string>.That.Matches(p => p.StartsWith("advfirewall firewall add rule", StringComparison.Ordinal)
                                            && p.Contains($"program=\"{_programPath}\"", StringComparison.Ordinal)
                                            && p.Contains("dir=in", StringComparison.Ordinal)
                                            && p.Contains("action=allow", StringComparison.Ordinal)),
                true,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task EnsureInboundRuleAsync_RuleExistsForAnotherProgram_AddsRule()
    {
        // Arrange
        A.CallTo(() => _runner.RunAsync(A<string>._, false, A<CancellationToken>._))
            .Returns(new NetshResult(0, @"Program: D:\Old\ClipMate.App.exe"));
        A.CallTo(() => _runner.RunAsync(A<string>._, true, A<CancellationToken>._)).Returns(new NetshResult(0, string.Empty));

        // Act
        await _service.EnsureInboundRuleAsync();

        // Assert
        A.CallTo(() => _runner.RunAsync(A<string>._, true, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task EnsureInboundRuleAsync_UserDeclinesElevation_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        A.CallTo(() => _runner.RunAsync(A<string>._, false, A<CancellationToken>._)).Returns(new NetshResult(1, string.Empty));
        A.CallTo(() => _runner.RunAsync(A<string>._, true, A<CancellationToken>._)).Throws(new Win32Exception(1223));

        // Act
        var result = await _service.EnsureInboundRuleAsync();

        // Assert
        await Assert.That(result).IsFalse();
    }
}
