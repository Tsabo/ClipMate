using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base class for TemplateService tests containing shared setup and helper methods.
/// Tests cover: variable extraction, expansion with built-in variables ({DATE}, {TIME}, {USERNAME}, {COMPUTERNAME}),
/// date/time format strings, prompt variables, CRUD operations, and error handling.
/// </summary>
public partial class TemplateServiceTests
{
    private readonly Mock<ITemplateRepository> _mockRepository;

    public TemplateServiceTests()
    {
        _mockRepository = new Mock<ITemplateRepository>();
    }

    private TemplateService CreateTemplateService()
    {
        return new TemplateService(_mockRepository.Object);
    }
}
