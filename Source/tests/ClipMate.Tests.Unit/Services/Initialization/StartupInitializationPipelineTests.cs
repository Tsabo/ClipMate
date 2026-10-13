using ClipMate.App.Services.Initialization;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services.Initialization;

public class StartupInitializationPipelineTests
{
    private readonly ILogger<StartupInitializationPipeline> _logger;

    public StartupInitializationPipelineTests()
    {
        _logger = A.Fake<ILogger<StartupInitializationPipeline>>();
    }

    [Test]
    public async Task RunAsync_ShouldExecuteStepsInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var step1 = A.Fake<IStartupInitializationStep>();
        A.CallTo(() => step1.Name).Returns("Step 1");
        A.CallTo(() => step1.Order).Returns(10);
        A.CallTo(() => step1.InitializeAsync(A<CancellationToken>.Ignored))
            .Invokes(() => executionOrder.Add("Step 1"))
            .Returns(Task.CompletedTask);

        var step2 = A.Fake<IStartupInitializationStep>();
        A.CallTo(() => step2.Name).Returns("Step 2");
        A.CallTo(() => step2.Order).Returns(20);
        A.CallTo(() => step2.InitializeAsync(A<CancellationToken>.Ignored))
            .Invokes(() => executionOrder.Add("Step 2"))
            .Returns(Task.CompletedTask);

        var step3 = A.Fake<IStartupInitializationStep>();
        A.CallTo(() => step3.Name).Returns("Step 3");
        A.CallTo(() => step3.Order).Returns(30);
        A.CallTo(() => step3.InitializeAsync(A<CancellationToken>.Ignored))
            .Invokes(() => executionOrder.Add("Step 3"))
            .Returns(Task.CompletedTask);

        // Register in wrong order to verify sorting
        var steps = new[] { step3, step1, step2 };
        var pipeline = new StartupInitializationPipeline(steps, _logger);

        // Act
        await pipeline.RunAsync();

        // Assert
        await Assert.That(executionOrder).IsEquivalentTo(["Step 1", "Step 2", "Step 3"]);
    }

    [Test]
    public async Task RunAsync_WhenStepFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var failingStep = A.Fake<IStartupInitializationStep>();
        A.CallTo(() => failingStep.Name).Returns("Failing Step");
        A.CallTo(() => failingStep.Order).Returns(10);
        A.CallTo(() => failingStep.InitializeAsync(A<CancellationToken>.Ignored))
            .ThrowsAsync(new InvalidOperationException("Step failed"));

        var steps = new[] { failingStep };
        var pipeline = new StartupInitializationPipeline(steps, _logger);

        // Act & Assert - should throw InvalidOperationException with step name in message
        await Assert.That(async () => await pipeline.RunAsync())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RunAsync_WithNoSteps_ShouldCompleteSuccessfully()
    {
        // Arrange
        var steps = Array.Empty<IStartupInitializationStep>();
        var pipeline = new StartupInitializationPipeline(steps, _logger);

        // Act & Assert - should not throw
        await pipeline.RunAsync();
    }

    [Test]
    public async Task RunAsync_ShouldPassCancellationTokenToSteps()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var receivedToken = CancellationToken.None;

        var step = A.Fake<IStartupInitializationStep>();
        A.CallTo(() => step.Name).Returns("Test Step");
        A.CallTo(() => step.Order).Returns(10);
        A.CallTo(() => step.InitializeAsync(A<CancellationToken>.Ignored))
            .Invokes((CancellationToken p) => receivedToken = p)
            .Returns(Task.CompletedTask);

        var steps = new[] { step };
        var pipeline = new StartupInitializationPipeline(steps, _logger);

        // Act
        await pipeline.RunAsync(cts.Token);

        // Assert
        await Assert.That(receivedToken).IsEqualTo(cts.Token);
    }
}
