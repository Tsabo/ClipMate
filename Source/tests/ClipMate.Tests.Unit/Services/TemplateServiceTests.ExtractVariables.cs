using TUnit.Core;
using TUnit.Assertions.Extensions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for TemplateService ExtractVariables method.
/// </summary>
public partial class TemplateServiceTests
{
    [Test]
    public async Task ExtractVariables_WithNoVariables_ShouldReturnEmptyList()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Hello world, this is plain text with no variables.";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables).IsEmpty();
    }

    [Test]
    public async Task ExtractVariables_WithSingleVariable_ShouldReturnVariable()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Hello {NAME}, welcome!";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(1);
        await Assert.That(variables[0]).IsEqualTo("NAME");
    }

    [Test]
    public async Task ExtractVariables_WithMultipleVariables_ShouldReturnAllVariables()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Date: {DATE}, Time: {TIME}, User: {USERNAME}";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(3);
        await Assert.That(variables).Contains("DATE");
        await Assert.That(variables).Contains("TIME");
        await Assert.That(variables).Contains("USERNAME");
    }

    [Test]
    public async Task ExtractVariables_WithDuplicateVariables_ShouldReturnUniqueVariables()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "{NAME} said to {NAME} that {NAME} is great";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(1);
        await Assert.That(variables[0]).IsEqualTo("NAME");
    }

    [Test]
    public async Task ExtractVariables_WithFormattedVariable_ShouldExtractVariableName()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Today is {DATE:yyyy-MM-dd}";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(1);
        await Assert.That(variables[0]).IsEqualTo("DATE");
    }

    [Test]
    public async Task ExtractVariables_WithPromptVariable_ShouldExtractVariableName()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Hello {PROMPT:Enter your name}!";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(1);
        await Assert.That(variables[0]).IsEqualTo("PROMPT");
    }
}
