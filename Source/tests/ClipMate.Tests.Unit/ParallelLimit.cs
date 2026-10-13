using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<ClipMate.Tests.Unit.TestConcurrencyLimit>]

namespace ClipMate.Tests.Unit;

/// <summary>
/// Caps how many tests run concurrently. See FakeProxyTypeWarmup for why this project can't
/// safely run fully unbounded parallel (github.com/FakeItEasy/FakeItEasy/issues/1794).
/// </summary>
public record TestConcurrencyLimit : IParallelLimit
{
    public int Limit => 8;
}
