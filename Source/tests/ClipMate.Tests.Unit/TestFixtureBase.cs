using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit;

/// <summary>
/// Base class for unit tests providing common setup and utilities.
/// </summary>
public abstract class TestFixtureBase
{
    protected MockRepository MockRepository { get; }

    protected TestFixtureBase()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
    }

    /// <summary>
    /// Creates a mock ILogger{T} that can be used in tests without strict verification.
    /// </summary>
    protected ILogger<T> CreateLogger<T>()
    {
        return new Mock<ILogger<T>>(MockBehavior.Loose).Object;
    }

    /// <summary>
    /// Verifies all mock expectations were met.
    /// </summary>
    protected void VerifyAll()
    {
        MockRepository.VerifyAll();
    }
}
