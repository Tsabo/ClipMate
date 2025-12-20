using ClipMate.App.Services.Initialization;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services.Initialization;

public class StartupInitializationPipelineTests
{
    private readonly ILogger<StartupInitializationPipeline> _logger;

    public StartupInitializationPipelineTests()
    {
        _logger = new Mock<ILogger<StartupInitializationPipeline>>(MockBehavior.Loose).Object;
    }

    [Test]
    public async Task RunAsync_ShouldExecuteStepsInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var step1 = new Mock<IStartupInitializationStep>();
        step1.Setup(p => p.Name).Returns("Step 1");
        step1.Setup(p => p.Order).Returns(10);
        step1.Setup(p => p.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => executionOrder.Add("Step 1"));

        var step2 = new Mock<IStartupInitializationStep>();
        step2.Setup(p => p.Name).Returns("Step 2");
        step2.Setup(p => p.Order).Returns(20);
        step2.Setup(p => p.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => executionOrder.Add("Step 2"));

        var step3 = new Mock<IStartupInitializationStep>();
        step3.Setup(p => p.Name).Returns("Step 3");
        step3.Setup(p => p.Order).Returns(30);
        step3.Setup(p => p.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => executionOrder.Add("Step 3"));

        // Register in wrong order to verify sorting
        var steps = new[] { step3.Object, step1.Object, step2.Object };
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
        var failingStep = new Mock<IStartupInitializationStep>();
        failingStep.Setup(p => p.Name).Returns("Failing Step");
        failingStep.Setup(p => p.Order).Returns(10);
        failingStep.Setup(p => p.InitializeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Step failed"));

        var steps = new[] { failingStep.Object };
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

        var step = new Mock<IStartupInitializationStep>();
        step.Setup(p => p.Name).Returns("Test Step");
        step.Setup(p => p.Order).Returns(10);
        step.Setup(p => p.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<CancellationToken>(p => receivedToken = p);

        var steps = new[] { step.Object };
        var pipeline = new StartupInitializationPipeline(steps, _logger);

        // Act
        await pipeline.RunAsync(cts.Token);

        // Assert
        await Assert.That(receivedToken).IsEqualTo(cts.Token);
    }
}
