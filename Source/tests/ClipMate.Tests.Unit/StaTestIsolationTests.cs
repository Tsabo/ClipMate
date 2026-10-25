using System.Reflection;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit;

/// <summary>
/// Tests on an STA thread (WPF, the clipboard, hotkey windows) run one at a time: WPF can deadlock when several STA
/// threads load XAML for the first time at once, and the run then hangs instead of failing.
/// </summary>
public class StaTestIsolationTests
{
    /// <summary>
    /// The <see cref="NotInParallelAttribute" /> key every STA test class carries.
    /// </summary>
    public const string StaKey = "sta";

    [Test]
    public async Task EveryStaTestClass_IsNotInParallelWithTheOthers()
    {
        // Arrange
        var staClasses = typeof(StaTestIsolationTests).Assembly.GetTypes()
            .Where(p => p.GetMethods().Any(m => m.GetCustomAttributes().Any(a => a is TestExecutorAttribute<STAThreadExecutor>)));

        // Act
        var missing = staClasses
            .Where(p => p.GetCustomAttribute<NotInParallelAttribute>() is not { } attribute || !attribute.ConstraintKeys.Contains(StaKey))
            .Select(p => p.FullName)
            .ToList();

        // Assert
        await Assert.That(missing).IsEmpty()
            .Because($"STA test classes need [NotInParallel(StaTestIsolationTests.StaKey)]: {string.Join(", ", missing)}");
    }
}
