using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.Builders;

/// <summary>
/// Builder class for creating test entity instances with sensible defaults.
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Creates a Clip entity with default or custom values.
    /// </summary>
    public static Clip CreateClip(Guid? id = null,
        ClipType type = ClipType.Text,
        string textContent = "Test content",
        string? label = null,
        Guid? collectionId = null,
        Guid? folderId = null,
        string? sourceApplicationName = null,
        DateTime? capturedAt = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Type = type,
            TextContent = textContent,
            Label = label,
            CollectionId = collectionId,
            FolderId = folderId,
            SourceApplicationName = sourceApplicationName ?? "TestApp.exe",
            CapturedAt = capturedAt ?? DateTime.UtcNow,
            ContentHash = ComputeHash(textContent),
        };

    /// <summary>
    /// Creates a Collection entity with default or custom values.
    /// </summary>
    public static Collection CreateCollection(Guid? id = null,
        string name = "Test Collection",
        string? description = null,
        bool isActive = true) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Description = description,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
        };

    /// <summary>
    /// Creates a Folder entity with default or custom values.
    /// </summary>
    public static Folder CreateFolder(Guid? id = null,
        string name = "Test Folder",
        Guid? collectionId = null,
        Guid? parentFolderId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            CollectionId = collectionId ?? Guid.NewGuid(),
            ParentFolderId = parentFolderId,
            CreatedAt = DateTime.UtcNow,
        };

    /// <summary>
    /// Creates a Template entity with default or custom values.
    /// </summary>
    public static Template CreateTemplate(Guid? id = null,
        string name = "Test Template",
        string content = "{{clip}}",
        Guid? collectionId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Content = content,
            CollectionId = collectionId,
            CreatedAt = DateTime.UtcNow,
        };

    /// <summary>
    /// Creates an ApplicationFilter entity with default or custom values.
    /// </summary>
    public static ApplicationFilter CreateApplicationFilter(Guid? id = null,
        string name = "Test Filter",
        string processName = "notepad.exe",
        string? windowTitlePattern = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            ProcessName = processName,
            WindowTitlePattern = windowTitlePattern,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        };

    /// <summary>
    /// Creates a SearchQuery entity with default or custom values.
    /// </summary>
    public static SearchQuery CreateSearchQuery(Guid? id = null,
        string name = "Test Search",
        string queryText = "test",
        bool isCaseSensitive = false,
        bool isRegex = false) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            QueryText = queryText,
            IsCaseSensitive = isCaseSensitive,
            IsRegex = isRegex,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = DateTime.UtcNow,
        };


    /// <summary>
    /// Computes a simple hash for test content (mimics SHA256 but simplified).
    /// </summary>
    private static string ComputeHash(string content)
    {
        var hash = content.GetHashCode();

        return $"hash_{hash:X8}";
    }
}
