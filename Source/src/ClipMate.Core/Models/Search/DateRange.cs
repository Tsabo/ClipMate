namespace ClipMate.Core.Models.Search;

/// <summary>
/// Date range for search filtering.
/// </summary>
public record DateRange(DateTime? From, DateTime? To);
