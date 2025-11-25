using ClipMate.Core.Repositories;
using Moq;

namespace ClipMate.Tests.Unit.Mocks;

/// <summary>
/// Factory for creating mock repository instances with common test data.
/// </summary>
public static class MockRepositoryFactory
{
    /// <summary>
    /// Creates a mock IClipRepository with optional setup.
    /// </summary>
    public static Mock<IClipRepository> CreateClipRepository(MockBehavior behavior = MockBehavior.Strict)
    {
        return new Mock<IClipRepository>(behavior);
    }

    /// <summary>
    /// Creates a mock ICollectionRepository with optional setup.
    /// </summary>
    public static Mock<ICollectionRepository> CreateCollectionRepository(MockBehavior behavior = MockBehavior.Strict)
    {
        return new Mock<ICollectionRepository>(behavior);
    }

    /// <summary>
    /// Creates a mock IFolderRepository with optional setup.
    /// </summary>
    public static Mock<IFolderRepository> CreateFolderRepository(MockBehavior behavior = MockBehavior.Strict)
    {
        return new Mock<IFolderRepository>(behavior);
    }

    /// <summary>
    /// Creates a mock ITemplateRepository with optional setup.
    /// </summary>
    public static Mock<ITemplateRepository> CreateTemplateRepository(MockBehavior behavior = MockBehavior.Strict)
    {
        return new Mock<ITemplateRepository>(behavior);
    }

    /// <summary>
    /// Creates a mock ISearchQueryRepository with optional setup.
    /// </summary>
    public static Mock<ISearchQueryRepository> CreateSearchQueryRepository(MockBehavior behavior = MockBehavior.Strict)
    {
        return new Mock<ISearchQueryRepository>(behavior);
    }

    /// <summary>
    /// Creates a mock IApplicationFilterRepository with optional setup.
    /// </summary>
    public static Mock<IApplicationFilterRepository> CreateApplicationFilterRepository(MockBehavior behavior = MockBehavior.Strict)
    {
        return new Mock<IApplicationFilterRepository>(behavior);
    }

    /// <summary>
    /// Creates a mock ISoundEventRepository with optional setup.
    /// </summary>
    public static Mock<ISoundEventRepository> CreateSoundEventRepository(MockBehavior behavior = MockBehavior.Strict)
    {
        return new Mock<ISoundEventRepository>(behavior);
    }
}
