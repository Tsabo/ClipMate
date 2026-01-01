namespace ClipMate.Core.Events;

/// <summary>
/// Request to resequence sort keys for collections in the current database.
/// Normalizes SortKey values to 10, 20, 30, etc. for cleaner ordering.
/// </summary>
public record ResequenceSortKeysRequestedEvent;
